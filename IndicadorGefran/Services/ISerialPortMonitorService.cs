using System;

namespace IndicadorGefran.Services
{
    public interface ISerialPortMonitorService
    {
        event EventHandler PortsChanged;
    }
}
