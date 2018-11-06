using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndicadorGefran.Model
{
    public class Reading : ICloneable
    {
        private readonly String value;
        private readonly TimeSpan time;

        public Reading(String value, DateTime initialTime) : this(value, DateTime.Now.Subtract(initialTime)) { }

        public Reading(String value, TimeSpan time)
        {
            this.value = value;
            this.time = time;
        }

        public String Value { get { return this.value; } }
        public TimeSpan Time { get { return this.time; } }

        public object Clone()
        {
            return new Reading(this.Value, this.Time);
        }
    }
}
