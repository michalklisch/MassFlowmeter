using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MassFlowmeter
{
    public partial class Flowmeter : Form
    {

        SerialPort mySerialPort;
        long initialTime = 0;
        bool connOK = false;

        private struct data
        {
            public long time;
            public long timeDelta;
            public float mass;
            public float delta;
            public float flowPerSecond;
            public float flowMedianFiltered;
            public float flowAverage;
        }

        List<data> results = new List<data>();
        DataTable dt = new DataTable();
        private delegate void SetResult(data d);
        private delegate void ShowConnection();

        public Flowmeter()
        {
            InitializeComponent();
            InitializeDataSource();
            InitializeForm();
        }

        private void GetSerialPorts()
        {
            try
            {
                this.portCombo.Items.Clear();
                foreach (string s in SerialPort.GetPortNames())
                {
                    this.portCombo.Items.Add(s);
                }
                if (this.portCombo.Items.Count == 1)
                {
                    this.portCombo.SelectedItem = this.portCombo.Items[0];
                    ConnectRS232();
                } 
            }
            catch (Exception ex)
            {
                LogData("GetSerialPorts", ex.Message);
            }
        }

        private void InitializeForm()
        {            
            this.baudText.Text = "9600";
            GetSerialPorts();   
        }

        private void InitializeDataSource()
        {
            dt.Columns.Add(new DataColumn("Time"));
            dt.Columns.Add(new DataColumn("TimeDelta"));
            dt.Columns.Add(new DataColumn("Mass"));
            dt.Columns.Add(new DataColumn("Delta"));
            dt.Columns.Add(new DataColumn("FlowPerSecond"));
            dt.Columns.Add(new DataColumn("FlowFiltered"));
            this.DataGrid.DataSource = dt;
        }

        private void AddDataToTable(data d)
        {
            DataRow dr = dt.NewRow();
            dr["Time"] = d.time;
            dr["TimeDelta"] = d.timeDelta;
            dr["Mass"] = d.mass;
            dr["Delta"] = d.delta;
            dr["FlowPerSecond"] = d.flowPerSecond;
            dr["FlowFiltered"] = d.flowAverage;
            dt.Rows.Add(dr);
        }

        public void ConnectRS232()
        {
            try
            {
                mySerialPort = new SerialPort(this.portCombo.SelectedItem.ToString());

                mySerialPort.BaudRate = int.Parse(this.baudText.Text);
                mySerialPort.Parity = Parity.None;
                mySerialPort.StopBits = StopBits.One;
                mySerialPort.DataBits = 8;
                mySerialPort.Handshake = Handshake.None;
                mySerialPort.ReadTimeout = 2000;
                mySerialPort.WriteTimeout = 500;

                mySerialPort.DtrEnable = true;
                mySerialPort.RtsEnable = true;
                mySerialPort.Open();
            }
            catch (Exception ex)
            {
                
                LogData("ConnectRS232", ex.Message);
            }

        }

        private void CheckConnection()
        {
            try
            {
                byte[] buff = new byte[4];
                buff[0] = Convert.ToByte('S');
                buff[1] = Convert.ToByte('J');
                buff[2] = Convert.ToByte('\r');
                buff[3] = Convert.ToByte('\n');
                mySerialPort.Write(buff, 0, 4);
            }
            catch (Exception ex)
            {
                LogData("CheckConnection", ex.Message);
            }
        }

        private void CheckReply(char[] p)
        {
            try
            {
                if (p[0] == 'M'
                    && p[1] == 'J')
                {
                    //conn OK
                    connOK = true;
                    this.BeginInvoke(new ShowConnection(ShowConn));
                }
            }
            catch (Exception ex)
            {
                LogData("CheckReply", ex.Message);
            }
        }

        private void ShowConn()
        {
            this.toolStripStatusLabel1.Text = "Connected on " + mySerialPort.PortName + " Baud rate: " + mySerialPort.BaudRate.ToString();
        }

        public void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                SerialPort sp = (SerialPort)sender;
                System.Threading.Thread.Sleep(10);
                string dataReceived = sp.ReadLine();
                sp.DiscardInBuffer();
                float result;
                if (dataReceived.Length > 5)
                {
                    dataReceived = dataReceived.Trim().Substring(0, 5).Replace('.', ',');
                }
                else
                    dataReceived = dataReceived.Trim();
                if (!float.TryParse(dataReceived, out result))
                {
                    CheckReply(dataReceived.ToCharArray());
                }
                else
                {
                    long curTime = DateTime.Now.Ticks;
                    if (initialTime != 0)
                        curTime = (curTime - initialTime) / TimeSpan.TicksPerMillisecond;
                    else
                    {
                        initialTime = curTime;
                        curTime = 0;
                    }
                    float delta = results.Count > 0 ? result - results[results.Count - 1].mass : result;
                    long timeDelta = results.Count > 0 ? curTime - results[results.Count - 1].time : curTime;
                    data tmp = new data();
                    tmp.time = curTime;
                    tmp.timeDelta = timeDelta;
                    tmp.mass = result;
                    tmp.delta = delta;
                    tmp.flowPerSecond = GetFlowPerSecond(delta, timeDelta);
                    tmp.flowMedianFiltered = MedianFilter(tmp.flowPerSecond);
                    tmp.flowAverage = CalculateAvgFlow();
                    results.Add(tmp);
                    this.BeginInvoke(new SetResult(addResult), new object[] { tmp });
                }
            }
            catch (Exception ex)
            {
                LogData("DataReceivedHandler", ex.Message);
            }
        }

        private float MedianFilter(float curVal)
        {
            try
            {
                List<float> medianBase = new List<float>();
                int i = 0;
                for (i = 0; i < (5 < results.Count ? 5 : results.Count); i++)
                    medianBase.Add(results[results.Count - 1 - i].flowPerSecond);
                while (i < 5)
                {
                    medianBase.Add(curVal);
                    i++;
                }
                medianBase.Sort();
                return medianBase[2];
            }
            catch (Exception ex)
            {
                LogData("MedianFilter", ex.Message);
            }
            return curVal;
        }

        private float CalculateAvgFlow()
        {
            float sum = 0;
            int i = 0;
            for (i = 0; i < (10 < results.Count ? 10 : results.Count); i++)
                sum += results[results.Count - 1 - i].flowMedianFiltered;
            return sum / i;
        }

        private void LogData(string method, string message)
        {
            StreamWriter sw = new StreamWriter("log.txt", true);
            sw.Write(String.Format("{0}\t| {1}\t| {2}\n", DateTime.Now.ToString(), method, message));
            sw.Close();
        }

        private void addResult(data d)
        {
            try
            {
                AddDataToTable(d);
                this.Chart.Series[0].Points.AddXY((float)d.time / 1000, d.flowAverage);
                this.Chart.ChartAreas[0].AxisY.Maximum = this.Chart.ChartAreas[0].AxisY.Maximum < d.flowAverage ? d.flowAverage * 1.3 : this.Chart.ChartAreas[0].AxisY.Maximum;
                this.Chart.Invalidate();
            }
            catch (Exception ex)
            {
                LogData("addResult", ex.Message);
            }
        }

        private float GetMaxFlow()
        {
            try
            {
                float maxFlow = 0;
                foreach (data d in results)
                {
                    if (d.flowAverage > maxFlow)
                        maxFlow = d.flowAverage;
                }
                return maxFlow;
            }
            catch (Exception ex)
            {
                LogData("GetMaxFlow", ex.Message);
            }            
            return 0;            
        }

        private float GetFlowPerSecond(float delta, long timeDelta)
        {
            try
            {
                if (timeDelta != 0)
                    return delta * 1000 / timeDelta;
            }
            catch (Exception ex)
            {
                LogData("GetFlowPerSecond", ex.Message);
            }
            return 0;
        }

        private void Connect_Click(object sender, EventArgs e)
        {
            ConnectRS232();
        }

        private void Refresh_Click(object sender, EventArgs e)
        {
            GetSerialPorts();
        }

        private void Start_Click(object sender, EventArgs e)
        {
            try
            {
                if (mySerialPort != null)
                {
                    if (!mySerialPort.IsOpen)
                        mySerialPort.Open();
                    mySerialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
                    while (!connOK)
                        CheckConnection();
                }
            }
            catch (Exception ex)
            {
                LogData("Start_Click", ex.Message);
            }
        }

        private void Stop_Click(object sender, EventArgs e)
        {
            try
            {
                if (mySerialPort.IsOpen)
                    mySerialPort.Close();
                connOK = false;
                Chart.Legends[0].CustomItems.Clear();
                System.Windows.Forms.DataVisualization.Charting.LegendItem l = new System.Windows.Forms.DataVisualization.Charting.LegendItem();
                l.Name = "Przepływ Max = " + Math.Round(GetMaxFlow(), 1).ToString() + " [ml/s]";
                Chart.Legends[0].CustomItems.Add(l);
                System.Windows.Forms.DataVisualization.Charting.LegendItem v = new System.Windows.Forms.DataVisualization.Charting.LegendItem();
                v.Name = "Objętość = " + Math.Round(results[results.Count - 1].mass, 1).ToString() + " [ml]";
                Chart.Legends[0].CustomItems.Add(v);
            }
            catch (Exception ex)
            {
                LogData("Stop_Click", ex.Message);
            }
        }

        private void Clear_Click(object sender, EventArgs e)
        {
            try
            {
                dt.Clear();
                results.Clear();
                initialTime = 0;
                this.Chart.ChartAreas[0].Axes[0].Minimum = 0;
                this.Chart.ChartAreas[0].Axes[1].Maximum = 10;
                this.Chart.Series[0].Points.Clear();
                this.Chart.Legends[0].CustomItems.Clear();
            }
            catch (Exception ex)
            {
                LogData("Clear_Click", ex.Message);
            }
        }

        private void SaveJPG_Click(object sender, EventArgs e)
        {
            try
            {
                saveFileDialog1.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                saveFileDialog1.AddExtension = true;
                saveFileDialog1.Filter = "jpg images (*.jpg)|*.jpg";
                saveFileDialog1.DefaultExt = "jpg";
                if (saveFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    Chart.SaveImage(saveFileDialog1.FileName, System.Drawing.Imaging.ImageFormat.Jpeg);
            }
            catch (Exception ex)
            {
                LogData("SaveJPG_Click", ex.Message);
            }
        }

        private void Clipboard_Click(object sender, EventArgs e)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    Chart.SaveImage(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                    Bitmap bm = new Bitmap(ms);
                    Clipboard.SetImage(bm);
                }
            }
            catch (Exception ex)
            {
                LogData("Clipboard_Click", ex.Message);
            }
        }

        private void SaveCSV_Click(object sender, EventArgs e)
        {
            try
            {
                saveFileDialog1.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                saveFileDialog1.AddExtension = true;
                saveFileDialog1.Filter = "csv files (*.csv)|*.csv";
                saveFileDialog1.DefaultExt = "csv";
                if (saveFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    StringBuilder sb = new StringBuilder();
                    IEnumerable<string> columnNames = dt.Columns.Cast<DataColumn>().
                                                      Select(column => column.ColumnName);
                    sb.AppendLine(string.Join(",", columnNames));
                    foreach (DataRow row in dt.Rows)
                    {
                        IEnumerable<string> fields = row.ItemArray.Select(field => field.ToString());
                        sb.AppendLine(string.Join(",", fields));
                    }
                    File.WriteAllText(saveFileDialog1.FileName, sb.ToString());
                }
            }
            catch (Exception ex)
            {
                LogData("SaveJPG_Click", ex.Message);
            }
            
        }

        private void OffsetChart_Click(object sender, EventArgs e)
        {
            foreach (data d in results)
            {
                if (d.flowAverage > 0)
                {
                    this.Chart.ChartAreas[0].Axes[0].Minimum = (float)d.time / 1000 - 10 > 0 ? Math.Round((float)d.time / 1000 - 5) : 0;
                    break;
                }
            }
        }

    }
}
