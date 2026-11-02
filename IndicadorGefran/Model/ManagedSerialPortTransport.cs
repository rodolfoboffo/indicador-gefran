using System;
using System.IO.Ports;

namespace IndicadorGefran.Model
{
    public class ManagedSerialPortTransport : ISerialPortTransport
    {
        private readonly SerialPort port;

        public ManagedSerialPortTransport(String portName)
        {
            this.port = new SerialPort(portName);
        }

        public String PortName { get { return this.port.PortName; } }

        public int BaudRate
        {
            get { return this.port.BaudRate; }
            set { this.port.BaudRate = value; }
        }

        public int DataBits
        {
            get { return this.port.DataBits; }
            set { this.port.DataBits = value; }
        }

        public Parity Parity
        {
            get { return this.port.Parity; }
            set { this.port.Parity = value; }
        }

        public StopBits StopBits
        {
            get { return this.port.StopBits; }
            set { this.port.StopBits = value; }
        }

        public int ReadTimeout
        {
            get { return this.port.ReadTimeout; }
            set { this.port.ReadTimeout = value; }
        }

        public bool IsOpen { get { return this.port.IsOpen; } }

        public void Open()
        {
            this.port.Open();
        }

        public void Close()
        {
            this.port.Close();
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            this.port.Write(buffer, offset, count);
        }

        public void DiscardInBuffer()
        {
            this.port.DiscardInBuffer();
        }

        public byte ReadByte(out bool parityError)
        {
            // SerialPort nao expoe erro de paridade por byte: o driver ja substitui
            // o byte corrompido por ParityReplace (0x3F por padrao) antes de chegar aqui.
            parityError = false;
            return (byte)this.port.ReadByte();
        }
    }
}
