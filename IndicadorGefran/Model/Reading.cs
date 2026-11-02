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
        private readonly bool isStale;

        public Reading(String value, DateTime initialTime) : this(value, DateTime.Now.Subtract(initialTime), false) { }

        public Reading(String value, TimeSpan time) : this(value, time, false) { }

        public Reading(String value, TimeSpan time, bool isStale)
        {
            this.value = value;
            this.time = time;
            this.isStale = isStale;
        }

        public String Value { get { return this.value; } }
        public TimeSpan Time { get { return this.time; } }

        /// <summary>Verdadeiro quando a medição é uma repetição da última leitura válida, registrada enquanto a conexão estava caindo/reconectando - não reflete uma leitura real feita nesse instante.</summary>
        public bool IsStale { get { return this.isStale; } }

        public String DisplayTime { get { return String.Format("{0:#0.000}", this.Time.TotalMilliseconds / 1000.0); } }

        public String Status { get { return this.isStale ? "Repetido (sem conexão)" : "OK"; } }

        public Reading AsStale()
        {
            return new Reading(this.value, this.time, true);
        }

        public object Clone()
        {
            return new Reading(this.Value, this.Time, this.isStale);
        }
    }
}
