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
            this.DataContext = new MainViewModel(new DialogService());
            this.Closing += OnMainWindowClosing;
        }

        private void OnMainWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ((MainViewModel)this.DataContext).OnWindowClosing();
        }
    }
}
