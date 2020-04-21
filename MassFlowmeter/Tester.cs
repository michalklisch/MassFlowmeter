using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassFlowmeter
{
    public class CustomEventArgs : EventArgs
    {
        public CustomEventArgs(float s)
        {
            result = s;
        }
        private float result;

        public float Result
        {
            get { return result; }
            set { result = value; }
        }
    }

    public class Tester
    {
        public Tester()
        {

        }

        public void DoWork()
        {
            DateTime startTime = DateTime.UtcNow;
            while (DateTime.UtcNow < startTime.AddSeconds(30))
            {
                System.Threading.Thread.Sleep(100);
                float result = (float)new Random().Next(100, 200) / 100;
                OnRaiseResultEvent(new CustomEventArgs(result));
            }
        }

        protected virtual void OnRaiseResultEvent(CustomEventArgs e)
        {
            EventHandler<CustomEventArgs> handler = RaiseResultEvent;


            // Event will be null if there are no subscribers 
            if (handler != null)
            {
                // Use the () operator to raise the event.
                handler(this, e);
            }
        }

        public event EventHandler<CustomEventArgs> RaiseResultEvent;

    }
}
