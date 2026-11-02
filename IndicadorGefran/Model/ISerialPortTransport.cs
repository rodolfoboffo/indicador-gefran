using System;
using System.IO.Ports;

namespace IndicadorGefran.Model
{
    public interface ISerialPortTransport
    {
        String PortName { get; }
        int BaudRate { get; set; }
        int DataBits { get; set; }
        Parity Parity { get; set; }
        StopBits StopBits { get; set; }
        int ReadTimeout { get; set; }
        bool IsOpen { get; }

        void Open();
        void Close();
        void Write(byte[] buffer, int offset, int count);
        void DiscardInBuffer();
        byte ReadByte(out bool parityError);
    }
}
