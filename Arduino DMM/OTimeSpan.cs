using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arduino_DMM
{
    public class OTimeSpan : ICloneable
    {
        public long Ticks;

        public OTimeSpan(long ticks)
        {
            this.Ticks = ticks;
        }

        public TimeSpan GetTimeSpan()
        {
            return new TimeSpan(Ticks);
        }

        public object Clone()
        {
            return new OTimeSpan(Ticks);
        }
    }
}
