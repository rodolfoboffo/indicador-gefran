using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO.Ports;
using System.Linq;
using IndicadorGefran.Model;

namespace IndicadorGefran
{
    public class SerialPortUtil
    {
        private static SerialPortUtil instance;

        private SerialPortUtil()
        {
        }

        public static SerialPortUtil Instance
        {
            get {
                if (instance == null)
                {
                    instance = new SerialPortUtil();
                }
                return instance;
            }
        }
   
        public List<String> AvailableSerialPortNames
        {
            get
            {
                string[] portNames = SerialPort.GetPortNames();
                return portNames.OfType<String>().ToList();
            }
        }

        public ISerialPortTransport GetSerialPort(String portName)
        {
            String transport = ConfigurationManager.AppSettings["SerialTransport"];
            if (String.Equals(transport, "Win32", StringComparison.OrdinalIgnoreCase))
                return new Win32SerialPortTransport(portName);
            return new ManagedSerialPortTransport(portName);
        }

        public Boolean IsPortAvailable(String portName)
        {
            return this.AvailableSerialPortNames.Contains(portName);
        }
    }
}
