using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassFlowmeter
{
    public struct data
    {
        public long time;
        public long timeDelta;
        public decimal mass;
        public decimal delta;
        public decimal flowPerSecond;
        public decimal flowMedianFiltered;
        public decimal flowAverage;
    }
}
