using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;

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
                if (instance is null)
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

        public SerialPort GetSerialPort(String portName)
        {
            SerialPort p = new SerialPort(portName);
            return p;
        }

        public Boolean IsPortAvailable(String portName)
        {
            return this.AvailableSerialPortNames.Contains(portName);
        }
    }
}
