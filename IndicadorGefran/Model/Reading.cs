using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndicadorGefran.Model
{
    public class Reading
    {
        private String value;
        private DateTime time;

        public Reading(String value)
        {
            this.value = value;
            this.time = DateTime.Now;
        }

        public String Value { get { return this.value; } }
        public DateTime Time { get { return this.time; } }
    }
}
