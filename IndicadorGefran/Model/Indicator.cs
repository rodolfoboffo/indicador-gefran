using System;
using System.IO.Ports;
using IndicadorGefran;
using IndicadorGefran.Model.Exceptions;
using System.Linq;
using System.Text;
using System.Timers;

namespace IndicadorGefran.Model
{
    public class Indicator
    {
        private static byte[] RESET_VECTOR = new byte[] { 0x55 };
        private static byte[] READ_COMMAND_VECTOR = new byte[] { 0x00 };
        private static byte[] GEFRAN_ADDRESS = new byte[] { 0x02, 0x82 };
        private static byte[] DISPLAY_ADDRESS = new byte[] { 0x03, 0x6F };

        private static Indicator instance;
        private Timer timer;
        private String value;
        private SerialPort port;
        public event EventHandler ConnectionStateChanged;
        public event EventHandler IndicatorValueChanged;

        private Indicator()
        {
            this.value = String.Empty;
            this.timer = new Timer(333);
            this.timer.Elapsed += OnTimerElapsed;
            this.ConnectionStateChanged += OnIndicatorConnectionStateChanged;
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            SwitchTimer(false);
            this.ReadDisplayValue();
            SwitchTimer(this.IsConnected);
        }

        private void OnIndicatorConnectionStateChanged(object sender, EventArgs e)
        {
            SwitchTimer(this.IsConnected);
        }

        private void SwitchTimer(Boolean on)
        {
            lock (this.timer)
            {
                this.timer.Enabled = on;
            }
        }

        public static Indicator Instance
        {
            get
            {
                if (instance is null)
                {
                    instance = new Indicator();
                }
                return instance;
            }
        }

        public String DisplayValue
        {
            get
            {
                return this.value;
            }
        }

        public Boolean IsConnected
        {
            get
            {
                if (this.port == null) return false;
                return this.port.IsOpen;
            }
        }

        public String PortName {
            get
            {
                if (this.port != null)
                    return this.port.PortName;
                return String.Empty;
            }
            set {
                this.port = SerialPortUtil.Instance.GetSerialPort(value);
            }
        }

        public void Initialize(String portName)
        {
            if (!SerialPortUtil.Instance.IsPortAvailable(portName))
                throw new SerialPortInvalidException();
            this.port = SerialPortUtil.Instance.GetSerialPort(portName);
            try
            {
                ExecuteInitializationProtocol(this.port);
                
                this.OnConnectionStateChanged(new EventArgs());
            }
            catch (Exception ex)
            {
                Disconnect();
                throw ex;
            }
        }

        public String ReadDisplayValue()
        {
            try
            {
                byte[] response = ReadAddress(this.port, 1, DISPLAY_ADDRESS, 7);
                String responseString = Encoding.ASCII.GetString(response.Reverse().ToArray());
                this.value = responseString;
                this.OnIndicatorValueChanged(new EventArgs());
                return responseString;
            }
            catch (Exception ex)
            {
                Disconnect();
                throw ex;
            }
        }

        private void ExecuteInitializationProtocol(SerialPort port)
        {
            OpenPort(port, Parity.Even);
            ResetSlave(port);
            port.Close();
            OpenPort(port, Parity.Odd);
            byte[] response = ReadAddress(port, 1, GEFRAN_ADDRESS, 6);
            String responseString = Encoding.ASCII.GetString(response.Reverse().ToArray());
            if (!responseString.Equals("GEFRAN")) throw new InvalidResponseFromIndicatorException();
        }

        private void OpenPort(SerialPort port, Parity parity)
        {
            port.ReadTimeout = 1000;
            port.BaudRate = 9600;
            port.DataBits = 8;
            port.StopBits = StopBits.One;
            port.Parity = parity;
            port.Open();
        }

        private void ResetSlave(SerialPort port)
        {
            WriteSerial(RESET_VECTOR, port);
            WriteSerial(RESET_VECTOR, port);
        }

        private void SelectSlave(SerialPort port, int slaveCode)
        {
            byte[] slaveCodeBytes = GetByteArray(slaveCode, 2);
            WriteAndCheckResponse(port, slaveCodeBytes, true);
        }

        private byte[] GetByteArray(int value, int numBytes)
        {
            byte[] output = new byte[numBytes];
            byte[] fullConversion = BitConverter.GetBytes(value).Reverse().ToArray();
            Array.Copy(fullConversion, fullConversion.Length - numBytes, output, 0, numBytes);
            return output;
        }

        private void WriteAndCheckResponse(SerialPort port, byte[] buffer, Boolean inverse)
        {
            byte[] expectedResponse = inverse ? NegateByteArray(buffer) : buffer;
            WriteSerial(buffer, port);
            byte[] response = GetResponse(buffer.Length, port);
            if (!response.SequenceEqual(expectedResponse)) throw new InvalidResponseFromIndicatorException();
        }

        private void SendCommandRead(SerialPort port)
        {
            WriteAndCheckResponse(port, READ_COMMAND_VECTOR, true);
        }

        private byte[] ReadAddress(SerialPort port, int slaveCode, byte[] address, int numBytes)
        {
            SelectSlave(port, slaveCode);
            SendCommandRead(port);
            SendNumberBytes(port, numBytes);
            SendAddress(port, address);
            byte[] response = GetResponse(numBytes, port);
            return response;
        }

        private void SendNumberBytes(SerialPort port, int numBytes)
        {
            byte[] b = GetByteArray(numBytes, 2);
            WriteAndCheckResponse(port, b, false);
        }

        private void SendAddress(SerialPort port, byte[] address)
        {
            WriteAndCheckResponse(port, address, false);
        }

        private byte[] NegateByteArray(byte[] input)
        {
            byte[] output = new byte[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                output[i] = (byte)~input[i];
            }
            return output;
        }

        private byte[] GetResponse(int numBytes, SerialPort port)
        {
            byte[] response = new byte[numBytes];
            try
            {
                for (int i = 0; i < numBytes; i++)
                {
                    response[i] = (byte)port.ReadByte();
                }
                return response;
            }
            catch (TimeoutException)
            {
                throw new NoResponseFromIndicatorException();
            }
        }

        private void WriteSerial(byte[] buffer, SerialPort port)
        {
            port.Write(buffer, 0, buffer.Length);
        }

        public void Disconnect()
        {
            if (this.port == null) return;
            if (this.port.IsOpen)
            {
                try
                {
                    this.port.Close();
                }
                finally
                {
                    this.port = null;
                    this.OnConnectionStateChanged(new EventArgs());
                }
            }
        }

        protected virtual void OnConnectionStateChanged(EventArgs e)
        {
            if (ConnectionStateChanged != null)
                ConnectionStateChanged(this, e);
        }

        protected virtual void OnIndicatorValueChanged(EventArgs e)
        {
            if (IndicatorValueChanged != null)
                IndicatorValueChanged(this, e);
        }
    }
}
