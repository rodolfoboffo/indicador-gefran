using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
