using System;
using System.IO;
using System.IO.Ports;
using System.Linq;

namespace IndicadorGefran.Model
{
    public class SerialPortLogger
    {
        private static SerialPortLogger instance;
        private readonly object writerLock = new object();
        private StreamWriter writer;
        private bool enabled;

        private SerialPortLogger()
        {
        }

        public static SerialPortLogger Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new SerialPortLogger();
                }
                return instance;
            }
        }

        public bool Enabled
        {
            get { return this.enabled; }
            set
            {
                if (this.enabled == value)
                    return;
                lock (this.writerLock)
                {
                    if (value)
                    {
                        String fileName = "serialport_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log";
                        String filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                        this.writer = new StreamWriter(filePath, false) { AutoFlush = true };
                    }
                    else
                    {
                        if (this.writer != null)
                        {
                            this.writer.Close();
                            this.writer = null;
                        }
                    }
                    this.enabled = value;
                }
            }
        }

        public void LogPortOpened(SerialPort port)
        {
            WriteLine("[OPEN] ", DescribePort(port));
        }

        public void LogPortClosed(SerialPort port)
        {
            WriteLine("[CLOSE]", DescribePort(port));
        }

        public void LogBytesSent(SerialPort port, byte[] bytes)
        {
            WriteLine("[TX]   ", DescribePort(port) + " Bytes=" + FormatHex(bytes));
        }

        public void LogBytesReceived(SerialPort port, byte[] bytes)
        {
            WriteLine("[RX]   ", DescribePort(port) + " Bytes=" + FormatHex(bytes));
        }

        private void WriteLine(String tag, String message)
        {
            if (!this.enabled)
                return;
            String line = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + tag + " " + message;
            lock (this.writerLock)
            {
                if (this.writer != null)
                    this.writer.WriteLine(line);
            }
        }

        private static String DescribePort(SerialPort port)
        {
            return String.Join(",", port.PortName, port.BaudRate.ToString(), DescribeParity(port.Parity), DescribeStopBits(port.StopBits));
        }

        private static String DescribeParity(Parity parity)
        {
            switch (parity)
            {
                case Parity.Even: return "E";
                case Parity.Odd: return "O";
                case Parity.Mark: return "M";
                case Parity.Space: return "S";
                default: return "N";
            }
        }

        private static String DescribeStopBits(StopBits stopBits)
        {
            switch (stopBits)
            {
                case StopBits.One: return "1";
                case StopBits.OnePointFive: return "1.5";
                case StopBits.Two: return "2";
                default: return "0";
            }
        }

        private static String FormatHex(byte[] bytes)
        {
            return String.Join(" ", bytes.Select(b => b.ToString("X2")));
        }
    }
}
