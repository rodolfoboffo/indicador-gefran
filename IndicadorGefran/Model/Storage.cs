using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.Windows;

namespace IndicadorGefran.Model
{
    public class Storage
    {
        private List<Reading> readings;
        public event EventHandler StorageChanged;

        public Storage()
        {
            this.readings = new List<Reading>();
        }

        protected virtual void OnStorageChanged(EventArgs e)
        {
            if (StorageChanged != null)
                StorageChanged(this, e);
        }

        public void AddReading(Reading r)
        {
            lock (this.readings)
            {
                this.readings.Add(r);
                this.OnStorageChanged(new EventArgs());
            }
        }

        public void Clear()
        {
            lock (this.readings)
            {
                this.readings.Clear();
                this.OnStorageChanged(new EventArgs());
            }
        }

        public List<Reading> Readings
        {
            get
            {
                return this.readings;
            }
        }

        public void ExportToCSV(String fileName)
        {
            StringBuilder builder = new StringBuilder();
            lock (this.readings)
            {
                foreach (Reading item in this.readings)
                {
                    builder.Append(String.Format("{0:#0.000}", item.Time.TotalMilliseconds*1000) + ";" + item.Value + "\n");
                }
            }
            try
            {
                StreamWriter file = new System.IO.StreamWriter(fileName);
                file.Write(builder.ToString());
                file.Close();
            }
            catch (Exception ex)
            {
                ((App)Application.Current).ShowError(ex.Message);
            }
        }
    }
}
