using System;
using System.Windows;
using System.Windows.Interop;

namespace IndicadorGefran.Services
{
    public class SerialPortMonitorService : ISerialPortMonitorService
    {
        private const int WM_DEVICECHANGE = 0x0219;

        private static SerialPortMonitorService instance;
        private HwndSource hwndSource;

        private SerialPortMonitorService()
        {
        }

        public static SerialPortMonitorService Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new SerialPortMonitorService();
                }
                return instance;
            }
        }

        public event EventHandler PortsChanged;

        public void Attach(Window window)
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            this.hwndSource = HwndSource.FromHwnd(handle);
            if (this.hwndSource != null)
                this.hwndSource.AddHook(WndProc);
        }

        public void Detach()
        {
            if (this.hwndSource != null)
            {
                this.hwndSource.RemoveHook(WndProc);
                this.hwndSource = null;
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_DEVICECHANGE)
            {
                OnPortsChanged();
            }
            return IntPtr.Zero;
        }

        private void OnPortsChanged()
        {
            if (PortsChanged != null)
                PortsChanged(this, EventArgs.Empty);
        }
    }
}
