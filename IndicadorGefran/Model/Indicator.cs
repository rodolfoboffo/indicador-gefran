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
    public enum IndicatorConnectionState
    {
        Disconnected,
        Connecting,
        Connected
    }

    public class Indicator
    {
        private static byte[] RESET_VECTOR = new byte[] { 0x55, 0x55, 0x55, 0x55, 0x55 };
        private static byte[] READ_COMMAND_VECTOR = new byte[] { 0x00 };
        private static byte[] GEFRAN_ADDRESS = new byte[] { 0x02, 0x82 };
        private static byte[] DISPLAY_ADDRESS = new byte[] { 0x03, 0x6F };
        private static byte[] ABSOLUTE_COUNT_ADDRESS = new byte[] { 0x03, 0x1A };

        private static Indicator instance;
        private DateTime initialTime;
        private Timer readingTimer;
        private Timer storageTimer;
        private Timer reconnectTimer;
        private Storage storage;
        private Reading reading;
        private ISerialPortTransport port;
        private int baudRate;
        private String currentPortName;
        private bool isRunning;
        private IndicatorConnectionState connectionState;
        public event EventHandler ConnectionStateChanged;
        public event EventHandler IndicatorValueChanged;

        private Indicator()
        {
            this.baudRate = 9600;
            this.storage = new Storage();
            this.reading = null;
            this.connectionState = IndicatorConnectionState.Disconnected;
            this.readingTimer = new Timer(1);
            this.storageTimer = new Timer(3000);
            this.reconnectTimer = new Timer(5);
            this.readingTimer.Elapsed += OnReadingTimerElapsed;
            this.storageTimer.Elapsed += OnStorageTimerElapsed;
            this.reconnectTimer.Elapsed += OnReconnectTimerElapsed;
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

        public bool IsRunning { get { return this.isRunning; } }

        public IndicatorConnectionState ConnectionState { get { return this.connectionState; } }

        private void OnStorageTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (this.reading == null)
                return;
            bool connected = this.connectionState == IndicatorConnectionState.Connected;
            this.storage.AddReading(connected ? this.reading : this.reading.AsStale());
        }

        private void AddReadingIntoStorage()
        {
            this.SwitchStorageTimer(false);
            this.storage.AddReading(this.reading);
            this.SwitchStorageTimer(this.isRunning);
        }

        private void OnReadingTimerElapsed(object sender, ElapsedEventArgs e)
        {
            SwitchReadingTimer(false);
            try
            {
                if (this.connectionState == IndicatorConnectionState.Connected)
                {
                    this.ReaAbsoluteCountValue();
                    if (this.Storage.Readings.Count == 0)
                        this.AddReadingIntoStorage();
                }
            }
            finally
            {
                SwitchReadingTimer(this.isRunning);
            }
        }

        private void OnIndicatorConnectionStateChanged(object sender, EventArgs e)
        {
            bool connected = this.connectionState == IndicatorConnectionState.Connected;
            SwitchReconnectTimer(this.isRunning && !connected);
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

        private void SwitchReconnectTimer(Boolean on)
        {
            lock (this.reconnectTimer)
            {
                this.reconnectTimer.Enabled = on;
            }
        }

        private void SetConnectionState(IndicatorConnectionState newState)
        {
            if (this.connectionState == newState)
                return;
            this.connectionState = newState;
            this.OnConnectionStateChanged(new EventArgs());
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

        public void Start(String portName, int baudRate)
        {
            this.isRunning = true;
            this.currentPortName = portName;
            this.baudRate = baudRate;
            this.RestartClock();
            SwitchReadingTimer(true);
            SwitchStorageTimer(true);
            this.SetConnectionState(IndicatorConnectionState.Connecting);
            this.AttemptConnect();
        }

        private void AttemptConnect()
        {
            ISerialPortTransport newPort = null;
            try
            {
                if (!SerialPortUtil.Instance.IsPortAvailable(this.currentPortName))
                    throw new SerialPortInvalidException();
                newPort = SerialPortUtil.Instance.GetSerialPort(this.currentPortName);
                ExecuteInitializationProtocol(newPort, this.baudRate);
            }
            catch (Exception)
            {
                TryClosePort(newPort);
                return;
            }

            if (!this.isRunning)
            {
                TryClosePort(newPort);
                return;
            }
            this.port = newPort;
            this.SetConnectionState(IndicatorConnectionState.Connected);
        }

        private void OnReconnectTimerElapsed(object sender, ElapsedEventArgs e)
        {
            SwitchReconnectTimer(false);
            try
            {
                this.AttemptConnect();
            }
            finally
            {
                bool connected = this.connectionState == IndicatorConnectionState.Connected;
                SwitchReconnectTimer(this.isRunning && !connected);
            }
        }

        private void HandleConnectionLost()
        {
            ISerialPortTransport p = this.port;
            this.port = null;
            TryClosePort(p);
            if (this.isRunning)
                this.SetConnectionState(IndicatorConnectionState.Connecting);
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
                HandleConnectionLost();
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
                HandleConnectionLost();
                throw ex;
            }
        }

        private void ExecuteInitializationProtocol(ISerialPortTransport port, int baudRate)
        {
            if (port.IsOpen)
                ClosePort(port);
            OpenPort(port, baudRate, Parity.Even);
            ResetSlave(port);
            ClosePort(port);
            OpenPort(port, baudRate, Parity.Odd);
            SelectSlave(port, 1);
            byte[] response = ReadAddress(port, baudRate, GEFRAN_ADDRESS, 6);
            String responseString = Encoding.ASCII.GetString(response.Reverse().ToArray());
            if (!responseString.Equals("GEFRAN")) throw new InvalidResponseFromIndicatorException();
        }

        private void OpenPort(ISerialPortTransport port, int baudRate, Parity parity)
        {
            port.ReadTimeout = 1000;
            port.BaudRate = baudRate;
            port.DataBits = 8;
            port.StopBits = StopBits.One;
            port.Parity = parity;
            port.Open();
            SerialPortLogger.Instance.LogPortOpened(port);
        }

        private void ClosePort(ISerialPortTransport port)
        {
            SerialPortLogger.Instance.LogPortClosed(port);
            port.Close();
        }

        private void TryClosePort(ISerialPortTransport port)
        {
            try
            {
                if (port != null && port.IsOpen)
                    ClosePort(port);
            }
            catch (Exception)
            {
            }
        }

        private void OpenPort(ISerialPortTransport port, int baudRate)
        {
            OpenPort(port, baudRate, Parity.Even);
        }

        private void ResetSlave(ISerialPortTransport port)
        {
            WriteSerial(RESET_VECTOR, port);
        }

        private void SelectSlave(ISerialPortTransport port, int slaveCode)
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

        private void WriteAndCheckResponse(ISerialPortTransport port, byte[] buffer, Boolean inverse)
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

        private void SendCommandRead(ISerialPortTransport port)
        {
            WriteAndCheckResponse(port, READ_COMMAND_VECTOR, true);
        }

        private byte[] ReadAddress(ISerialPortTransport port, int baudRate, byte[] address, int numBytes)
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

        private void SendNumberBytes(ISerialPortTransport port, int numBytes)
        {
            byte[] b = GetByteArray(numBytes, 2);
            WriteAndCheckResponse(port, b, false);
        }

        private void SendAddress(ISerialPortTransport port, byte[] address)
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

        private byte[] GetResponse(int numBytes, ISerialPortTransport port)
        {
            byte[] response = new byte[numBytes];
            bool[] parityErrors = new bool[numBytes];
            try
            {
                for (int i = 0; i < numBytes; i++)
                {
                    bool parityError;
                    response[i] = port.ReadByte(out parityError);
                    parityErrors[i] = parityError;
                }
                SerialPortLogger.Instance.LogBytesReceived(port, response, parityErrors);
                return response;
            }
            catch (TimeoutException)
            {
                throw new NoResponseFromIndicatorException();
            }
        }

        private void WriteSerial(byte[] buffer, ISerialPortTransport port)
        {
            port.Write(buffer, 0, buffer.Length);
            SerialPortLogger.Instance.LogBytesSent(port, buffer);
        }

        public void Stop()
        {
            this.isRunning = false;
            ISerialPortTransport p = this.port;
            this.port = null;
            TryClosePort(p);
            SwitchReadingTimer(false);
            SwitchStorageTimer(false);
            SwitchReconnectTimer(false);
            this.SetConnectionState(IndicatorConnectionState.Disconnected);
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
