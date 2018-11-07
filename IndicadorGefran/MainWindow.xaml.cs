using IndicadorGefran.Model;
using IndicadorGefran.Model.Exceptions;
using Microsoft.Win32;
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
            this.Closing += OnMainWindowClosing;
            Indicator.Instance.ConnectionStateChanged += OnIndicatorConnectionStateChanged;
            Indicator.Instance.IndicatorValueChanged += OnIndicatorValueChanged;
            Indicator.Instance.Storage.StorageChanged += OnStorageChanged;
        }

        private void OnMainWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Indicator.Instance.Terminate();
        }

        private void InitializeSliderTimerStorage()
        {
            this.sliderTimerStorage.Value = Indicator.Instance.StorageTimerInterval;
        }

        private void OnStorageChanged(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() => {
                UpdateListViewStorage();
            }));
        }

        private void InitializeListViewStorage()
        {
            this.datagridStorage.ItemsSource = Indicator.Instance.Storage.Readings;
        }

        private void UpdateListViewStorage()
        {
            this.datagridStorage.Items.Refresh();
        }

        private void OnIndicatorValueChanged(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() => {
                Reading r = Indicator.Instance.Reading;
                this.labelMainIndicator.Content = r != null ? r.Value : String.Empty;
                this.labelTime.Content = r != null ? r.DisplayTime : String.Empty;
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
            this.buttonRefreshSerialPorts.IsEnabled = !Indicator.Instance.IsReady;
            this.comboboxSerialPorts.IsEnabled = !Indicator.Instance.IsReady;
            this.comboboxBaudrates.IsEnabled = !Indicator.Instance.IsReady;
        }

        private void RefreshConnectDisconnectButtonLabel()
        {
            if (Indicator.Instance.IsReady)
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
            UpdateBaudRatesCombobox();
            InitializeListViewStorage();
            InitializeSliderTimerStorage();
            RefreshConnectDisconnectButtonLabel();
        }

        private void UpdateBaudRatesCombobox()
        {
            int[] baudrates = new int[] { 9600, 19200, 38400 };
            for (int i = 0; i < baudrates.Length; i++)
                this.comboboxBaudrates.Items.Add(baudrates[i]);
            this.comboboxBaudrates.SelectedIndex = 0;
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
            Object selectedPortName = this.comboboxSerialPorts.SelectedValue;
            int selectedBaudrate = (int)this.comboboxBaudrates.SelectedValue;
            if (!Indicator.Instance.IsReady)
            {
                if (selectedPortName == null)
                {
                    this.app.ShowAlert("Selecione uma porta serial.");
                    return;
                }
                try
                {
                    this.buttonConnectDisconnect.IsEnabled = false;
                    Indicator.Instance.Initialize(selectedPortName.ToString(), selectedBaudrate);
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

        private void OnButtonLimparClick(object sender, RoutedEventArgs e)
        {
            Indicator.Instance.Storage.Clear();
            Indicator.Instance.RestartClock();
        }

        private void OnButtonExportClick(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Comma-separated Values (*.csv)|*.csv";
            if (dialog.ShowDialog() == true)
            {
                Indicator.Instance.Storage.ExportToCSV(dialog.FileName);
            }
        }

        private void OnSliderTimerStorageValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Indicator.Instance.StorageTimerInterval = Convert.ToInt32(e.NewValue);
            this.labelSliderTimerStorageValue.Content = Convert.ToInt32(e.NewValue) + " s";
        }

        
    }
}
