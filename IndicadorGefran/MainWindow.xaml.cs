using IndicadorGefran.Services;
using IndicadorGefran.ViewModel;
using System.Windows;

namespace IndicadorGefran
{
    /// <summary>
    /// Interação lógica para MainWindow.xam
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new MainViewModel(DialogService.Instance, SerialPortMonitorService.Instance);
            this.SourceInitialized += OnMainWindowSourceInitialized;
            this.Closing += OnMainWindowClosing;
        }

        private void OnMainWindowSourceInitialized(object sender, System.EventArgs e)
        {
            SerialPortMonitorService.Instance.Attach(this);
        }

        private void OnMainWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SerialPortMonitorService.Instance.Detach();
            ((MainViewModel)this.DataContext).OnWindowClosing();
        }
    }
}
