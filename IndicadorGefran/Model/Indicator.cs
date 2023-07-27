using System;
using System.IO.Ports;
using IndicadorGefran;
using IndicadorGefran.Model.Exceptions;
using System.Linq;
using System.Text;
using System.Timers;
using System.Windows;
using System.Collections.Generic;

namespace IndicadorGefran.Model
{
    public class Indicator
    {
        private static byte[] RESET_VECTOR = new byte[] { 0x55 };
        private static byte[] READ_COMMAND_VECTOR = new byte[] { 0x00 };
        private static byte[] GEFRAN_ADDRESS = new byte[] { 0x02, 0x82 };
        private static byte[] DISPLAY_ADDRESS = new byte[] { 0x03, 0x6F };
        private static byte[] ABSOLUTE_COUNT_ADDRESS = new byte[] { 0x03, 0x1A };

        private static Indicator instance;
        private DateTime initialTime;
        private Timer readingTimer;
        private Timer storageTimer;
        private Storage storage;
        private Reading reading;
        private SerialPort port;
        private int baudRate;
        public event EventHandler ConnectionStateChanged;
        public event EventHandler IndicatorValueChanged;

        private Indicator()
        {
            this.baudRate = 9600;
            this.storage = new Storage();
            this.reading = null;
            this.readingTimer = new Timer(1);
            this.storageTimer = new Timer(3000);
            this.readingTimer.Elapsed += OnReadingTimerElapsed;
            this.storageTimer.Elapsed += OnStorageTimerElapsed;
            this.ConnectionStateChanged += OnIndicatorConnectionStateChanged;
            this.RestartClock();
        }

        public void RestartClock()
        {
            this.initialTime = DateTime.Now;
        }

        public int StorageTimerInterval
        {
            get
            {
                return Convert.ToInt32(this.storageTimer.Interval / 1000);
            }
            set
            {
                this.storageTimer.Interval = Convert.ToDouble(value*1000);
            }
        }

        private void OnStorageTimerElapsed(object sender, ElapsedEventArgs e)
        {
            this.storage.AddReading(this.reading);
        }

        private void AddReadingIntoStorage()
        {
            this.SwitchStorageTimer(false);
            this.storage.AddReading(this.reading);
            this.SwitchStorageTimer(this.IsReady);
        }

        private void OnReadingTimerElapsed(object sender, ElapsedEventArgs e)
        {
            SwitchReadingTimer(false);
            this.ReaAbsoluteCountValue();
            if (this.Storage.Readings.Count == 0)
                this.AddReadingIntoStorage();
            SwitchReadingTimer(this.IsReady);
        }

        private void OnIndicatorConnectionStateChanged(object sender, EventArgs e)
        {
            SwitchReadingTimer(this.IsReady);
            SwitchStorageTimer(this.IsReady);
        }

        private void SwitchReadingTimer(Boolean on)
        {
            lock (this.readingTimer)
            {
                this.readingTimer.Enabled = on;
            }
        }

        private void SwitchStorageTimer(Boolean on)
        {
            lock (this.storageTimer)
            {
                this.storageTimer.Enabled = on;
            }
        }

        public static Indicator Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new Indicator();
                }
                return instance;
            }
        }

        public Storage Storage { get { return this.storage; } }

        public Reading Reading
        {
            get
            {
                return this.reading;
            }
        }

        public Boolean IsReady
        {
            get
            {
                return this.port != null && this.port.IsOpen;
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

        public void Initialize(String portName, int baudRate)
        {
            if (!SerialPortUtil.Instance.IsPortAvailable(portName))
                throw new SerialPortInvalidException();
            this.port = SerialPortUtil.Instance.GetSerialPort(portName);
            this.baudRate = baudRate;
            try
            {
                ExecuteInitializationProtocol(this.port, this.baudRate);
                this.RestartClock();
                this.OnConnectionStateChanged(new EventArgs());
            }
            catch (Exception ex)
            {
                Stop();
                throw ex;
            }
        }

        public String ReadDisplayValue()
        {
            try
            {
                byte[] response = ReadAddress(this.port, this.baudRate, DISPLAY_ADDRESS, 7);
                String responseString = Encoding.ASCII.GetString(response.Reverse().ToArray());
                this.reading = new Reading(responseString, this.initialTime);
                //((App)Application.Current).ShowInfo(this.value);
                this.OnIndicatorValueChanged(new EventArgs());
                return responseString;
            }
            catch (Exception ex)
            {
                Stop();
                throw ex;
            }
        }

        public String ReaAbsoluteCountValue()
        {
            try
            {
                byte[] response = ReadAddress(this.port, this.baudRate, ABSOLUTE_COUNT_ADDRESS, 4);
                int value = BitConverter.ToInt32(response.Reverse().ToArray(), 0);
                String responseString = Convert.ToString(value);
                this.reading = new Reading(responseString, this.initialTime);
                //((App)Application.Current).ShowInfo(this.value);
                this.OnIndicatorValueChanged(new EventArgs());
                return responseString;
            }
            catch (Exception ex)
            {
                Stop();
                throw ex;
            }
        }

        private void ExecuteInitializationProtocol(SerialPort port, int baudRate)
        {
            if (port.IsOpen)
                port.Close();
            OpenPort(port, baudRate, Parity.Even);
            ResetSlave(port);
            port.Close();
            OpenPort(port, baudRate, Parity.Odd);
            SelectSlave(port, 1);
            byte[] response = ReadAddress(port, baudRate, GEFRAN_ADDRESS, 6);
            String responseString = Encoding.ASCII.GetString(response.Reverse().ToArray());
            if (!responseString.Equals("GEFRAN")) throw new InvalidResponseFromIndicatorException();
        }

        private void OpenPort(SerialPort port, int baudRate, Parity parity)
        {
            port.ReadTimeout = 1000;
            port.BaudRate = baudRate;
            port.DataBits = 8;
            port.StopBits = StopBits.One;
            port.Parity = parity;
            port.Open();
        }

        private void OpenPort(SerialPort port, int baudRate)
        {
            OpenPort(port, baudRate, Parity.Even);
        }

        private void ResetSlave(SerialPort port)
        {
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
            //StringBuilder builder = new StringBuilder();
            //builder.Append("Output: ");
            //for (int i = 0; i < buffer.Length; i++)
            //{
            //    builder.Append(buffer[i].ToString("X2"));
            //    builder.Append(buffer[i].ToString(" "));
            //}
            //((App)Application.Current).ShowInfo(builder.ToString());
            byte[] expectedResponse = inverse ? NegateByteArray(buffer) : buffer;
            WriteSerial(buffer, port);
            byte[] response = GetResponse(buffer.Length, port);
            //builder = new StringBuilder();
            //builder.Append("Input: ");
            //for (int i = 0; i < response.Length; i++)
            //{
            //    builder.Append(response[i].ToString("X2"));
            //    builder.Append(response[i].ToString(" "));
            //}
            //((App)Application.Current).ShowInfo(builder.ToString());
            if (!response.SequenceEqual(expectedResponse)) throw new InvalidResponseFromIndicatorException();
        }

        private void SendCommandRead(SerialPort port)
        {
            WriteAndCheckResponse(port, READ_COMMAND_VECTOR, true);
        }

        private byte[] ReadAddress(SerialPort port, int baudRate, byte[] address, int numBytes)
        {
            SendCommandRead(port);
            SendNumberBytes(port, numBytes);
            SendAddress(port, address);
            byte[] response = GetResponse(numBytes, port);
            //StringBuilder builder = new StringBuilder();
            //builder.Append("Read Value: ");
            //for (int i = 0; i < response.Length; i++)
            //{
            //    builder.Append(response[i].ToString("X2"));
            //    builder.Append(response[i].ToString(" "));
            //}
            //((App)Application.Current).ShowInfo(builder.ToString());
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

        public void Stop()
        {
            SerialPort p = this.port;
            this.port = null;
            if (p != null && p.IsOpen)
                p.Close();
            SwitchReadingTimer(false);
            SwitchStorageTimer(false);
            this.OnConnectionStateChanged(new EventArgs());
        }

        public void Terminate()
        {
            this.Stop();
            //this.readingTimer.Dispose();
            //this.storageTimer.Dispose();
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
