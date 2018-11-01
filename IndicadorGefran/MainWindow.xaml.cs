using IndicadorGefran.Model;
using IndicadorGefran.Model.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace IndicadorGefran
{
    /// <summary>
    /// Interação lógica para MainWindow.xam
    /// </summary>
    public partial class MainWindow : Window
    {

        App app;

        public MainWindow()
        {
            InitializeComponent();
            this.app = (App)Application.Current;
            this.Loaded += OnMainWindowLoaded;
            Indicator.Instance.ConnectionStateChanged += OnIndicatorConnectionStateChanged;
            Indicator.Instance.IndicatorValueChanged += OnIndicatorValueChanged;
            RefreshConnectDisconnectButtonLabel();
        }

        private void OnIndicatorValueChanged(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() => {
                this.labelMainIndicator.Content = Indicator.Instance.DisplayValue;
            }));
        }

        private void OnIndicatorConnectionStateChanged(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() => {
                RefreshToolbarState();
                RefreshConnectDisconnectButtonLabel();
            }));
        }

        private void RefreshToolbarState()
        {
            this.buttonRefreshSerialPorts.IsEnabled = !Indicator.Instance.IsConnected;
            this.comboboxSerialPorts.IsEnabled = !Indicator.Instance.IsConnected;
        }

        private void RefreshConnectDisconnectButtonLabel()
        {
            if (Indicator.Instance.IsConnected)
            {
                this.buttonConnectDisconnect.Content = "Desconectar";
            }
            else
            {
                this.buttonConnectDisconnect.Content = "Conectar";
            }
        }

        private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            UpdateSerialPortsCombobox();
        }

        private void UpdateSerialPortsCombobox()
        {
            this.comboboxSerialPorts.Items.Clear();
            SerialPortUtil.Instance.AvailableSerialPortNames.ForEach(
                name => { this.comboboxSerialPorts.Items.Add(name); }
                );
        }

        private void OnButtonConnectClick(object sender, RoutedEventArgs e)
        {
            Object selectedItem = this.comboboxSerialPorts.SelectedValue;
            if (!Indicator.Instance.IsConnected)
            {
                if (selectedItem == null)
                {
                    this.app.ShowAlert("Selecione uma porta serial.");
                    return;
                }
                try
                {
                    this.buttonConnectDisconnect.IsEnabled = false;
                    Indicator.Instance.Initialize(selectedItem.ToString());
                }
                catch (SerialPortInvalidException)
                {
                    this.app.ShowError("Esta porta serial já não é mais válida.");
                    UpdateSerialPortsCombobox();
                }
                catch (Exception ex)
                {
                    this.app.ShowError(ex.Message);
                }
                finally
                {
                    this.buttonConnectDisconnect.IsEnabled = true;
                }
            }
            else
            {
                Indicator.Instance.Disconnect();
            }
        }
    }
}
