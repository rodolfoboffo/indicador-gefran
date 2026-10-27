using System;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace IndicadorGefran.Model
{
    // Implementacao alternativa baseada direto na API Win32 (CreateFile/ReadFile/ClearCommError),
    // em vez de System.IO.Ports.SerialPort. O motivo de existir: SerialPort nao expoe, por byte,
    // se a leitura teve erro de paridade (CE_RXPARITY) - o driver so substitui o byte corrompido
    // por ParityReplace (0x3F por padrao) antes da aplicacao ver. Lendo byte a byte e chamando
    // ClearCommError imediatamente antes/depois de cada ReadFile, a flag fica correlacionada
    // com certeza a esse unico byte.
    public class Win32SerialPortTransport : ISerialPortTransport
    {
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        private const uint CE_RXPARITY = 0x0004;
        private const uint DCB_FBINARY = 0x1;
        private const uint DCB_FPARITY = 0x2;
        private const uint DCB_FERRORCHAR = 0x400;

        [StructLayout(LayoutKind.Sequential)]
        private struct DCB
        {
            public uint DCBlength;
            public uint BaudRate;
            public uint Flags;
            public ushort wReserved;
            public ushort XonLim;
            public ushort XoffLim;
            public byte ByteSize;
            public byte Parity;
            public byte StopBits;
            public byte XonChar;
            public byte XoffChar;
            public byte ErrorChar;
            public byte EofChar;
            public byte EvtChar;
            public ushort wReserved1;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct COMMTIMEOUTS
        {
            public uint ReadIntervalTimeout;
            public uint ReadTotalTimeoutMultiplier;
            public uint ReadTotalTimeoutConstant;
            public uint WriteTotalTimeoutMultiplier;
            public uint WriteTotalTimeoutConstant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct COMSTAT
        {
            public uint Flags;
            public uint cbInQue;
            public uint cbOutQue;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern SafeFileHandle CreateFile(String lpFileName, uint dwDesiredAccess, uint dwShareMode,
            IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetCommState(SafeFileHandle hFile, ref DCB lpDCB);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetCommState(SafeFileHandle hFile, ref DCB lpDCB);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetCommTimeouts(SafeFileHandle hFile, ref COMMTIMEOUTS lpCommTimeouts);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(SafeFileHandle hFile, byte[] lpBuffer, uint nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(SafeFileHandle hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite,
            out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ClearCommError(SafeFileHandle hFile, out uint lpErrors, out COMSTAT lpStat);

        private SafeFileHandle handle;
        private int baudRate = 9600;
        private int dataBits = 8;
        private Parity parity = Parity.None;
        private StopBits stopBits = StopBits.One;
        private int readTimeout = 500;

        public Win32SerialPortTransport(String portName)
        {
            this.PortName = portName;
        }

        public String PortName { get; private set; }

        public int BaudRate
        {
            get { return this.baudRate; }
            set { this.baudRate = value; ApplyDcbIfOpen(); }
        }

        public int DataBits
        {
            get { return this.dataBits; }
            set { this.dataBits = value; ApplyDcbIfOpen(); }
        }

        public Parity Parity
        {
            get { return this.parity; }
            set { this.parity = value; ApplyDcbIfOpen(); }
        }

        public StopBits StopBits
        {
            get { return this.stopBits; }
            set { this.stopBits = value; ApplyDcbIfOpen(); }
        }

        public int ReadTimeout
        {
            get { return this.readTimeout; }
            set { this.readTimeout = value; ApplyTimeoutsIfOpen(); }
        }

        public bool IsOpen { get { return this.handle != null && !this.handle.IsInvalid && !this.handle.IsClosed; } }

        public void Open()
        {
            SafeFileHandle h = CreateFile(@"\\.\" + this.PortName, GENERIC_READ | GENERIC_WRITE, 0,
                IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, IntPtr.Zero);
            if (h.IsInvalid)
                throw new IOException("Não foi possível abrir a porta " + this.PortName + " (Win32 erro " + Marshal.GetLastWin32Error() + ").");
            this.handle = h;
            ApplyDcbIfOpen();
            ApplyTimeoutsIfOpen();
        }

        public void Close()
        {
            if (this.handle != null)
            {
                this.handle.Close();
                this.handle = null;
            }
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            byte[] toWrite = buffer;
            if (offset != 0)
            {
                toWrite = new byte[count];
                Array.Copy(buffer, offset, toWrite, 0, count);
            }
            uint written;
            if (!WriteFile(this.handle, toWrite, (uint)count, out written, IntPtr.Zero) || written != count)
                throw new IOException("Falha ao escrever na porta " + this.PortName + " (Win32 erro " + Marshal.GetLastWin32Error() + ").");
        }

        public byte ReadByte(out bool parityError)
        {
            uint ignoredErrors;
            COMSTAT ignoredStat;
            ClearCommError(this.handle, out ignoredErrors, out ignoredStat);

            byte[] buffer = new byte[1];
            uint bytesRead;
            if (!ReadFile(this.handle, buffer, 1, out bytesRead, IntPtr.Zero))
                throw new IOException("Falha ao ler da porta " + this.PortName + " (Win32 erro " + Marshal.GetLastWin32Error() + ").");
            if (bytesRead == 0)
                throw new TimeoutException();

            uint errors;
            COMSTAT stat;
            ClearCommError(this.handle, out errors, out stat);
            parityError = (errors & CE_RXPARITY) != 0;

            return buffer[0];
        }

        private void ApplyDcbIfOpen()
        {
            if (!IsOpen)
                return;
            DCB dcb = new DCB();
            dcb.DCBlength = (uint)Marshal.SizeOf(typeof(DCB));
            if (!GetCommState(this.handle, ref dcb))
                throw new IOException("Falha ao ler configuração da porta " + this.PortName + " (Win32 erro " + Marshal.GetLastWin32Error() + ").");

            dcb.BaudRate = (uint)this.baudRate;
            dcb.ByteSize = (byte)this.dataBits;
            dcb.Parity = MapParity(this.parity);
            dcb.StopBits = MapStopBits(this.stopBits);
            dcb.Flags = (dcb.Flags | DCB_FBINARY | DCB_FPARITY) & ~DCB_FERRORCHAR;

            if (!SetCommState(this.handle, ref dcb))
                throw new IOException("Falha ao configurar a porta " + this.PortName + " (Win32 erro " + Marshal.GetLastWin32Error() + ").");
        }

        private void ApplyTimeoutsIfOpen()
        {
            if (!IsOpen)
                return;
            COMMTIMEOUTS timeouts = new COMMTIMEOUTS();
            timeouts.ReadIntervalTimeout = 0;
            timeouts.ReadTotalTimeoutMultiplier = 0;
            timeouts.ReadTotalTimeoutConstant = (uint)this.readTimeout;
            timeouts.WriteTotalTimeoutMultiplier = 0;
            timeouts.WriteTotalTimeoutConstant = 0;

            if (!SetCommTimeouts(this.handle, ref timeouts))
                throw new IOException("Falha ao configurar timeout da porta " + this.PortName + " (Win32 erro " + Marshal.GetLastWin32Error() + ").");
        }

        private static byte MapParity(Parity parity)
        {
            switch (parity)
            {
                case Parity.None: return 0;
                case Parity.Odd: return 1;
                case Parity.Even: return 2;
                case Parity.Mark: return 3;
                case Parity.Space: return 4;
                default: throw new ArgumentOutOfRangeException("parity");
            }
        }

        private static byte MapStopBits(StopBits stopBits)
        {
            switch (stopBits)
            {
                case StopBits.One: return 0;
                case StopBits.OnePointFive: return 1;
                case StopBits.Two: return 2;
                default: throw new ArgumentOutOfRangeException("stopBits");
            }
        }
    }
}
