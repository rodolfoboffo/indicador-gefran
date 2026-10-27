using IndicadorGefran.Model;
using IndicadorGefran.Model.Exceptions;
using IndicadorGefran.Services;
using System;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;

namespace IndicadorGefran.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IDialogService dialogService;

        private String selectedPortName;
        private int selectedBaudRate;
        private bool isConnectionSettingsEditable;
        private String connectButtonLabel;
        private String currentValue;
        private String currentTime;
        private String storageIntervalText;
        private bool isConnecting;

        public MainViewModel(IDialogService dialogService)
        {
            this.dialogService = dialogService;

            this.SerialPortNames = new ObservableCollection<String>();
            this.BaudRates = new ObservableCollection<int>(new int[] { 1200, 2400, 4800, 9600, 19200, 38400 });
            this.Readings = new ObservableCollection<Reading>();

            this.ConnectDisconnectCommand = new RelayCommand(_ => ExecuteConnectDisconnect(), _ => !this.isConnecting);
            this.RefreshPortsCommand = new RelayCommand(_ => RefreshSerialPortNames());
            this.ClearCommand = new RelayCommand(_ => ExecuteClear());
            this.ExportCommand = new RelayCommand(_ => ExecuteExport());

            Indicator.Instance.ConnectionStateChanged += OnIndicatorConnectionStateChanged;
            Indicator.Instance.IndicatorValueChanged += OnIndicatorValueChanged;
            Indicator.Instance.Storage.StorageChanged += OnStorageChanged;

            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            this.WindowTitle = String.Format("Software para coleta de tempo e deslocamento Indicador Gefran 2351 - rev. {2}",
                version.Major, version.Minor, version.MinorRevision);

            RefreshSerialPortNames();
            this.selectedBaudRate = this.BaudRates[1];
            RefreshConnectionDependentState();
            RefreshStorageIntervalText();
        }

        public ObservableCollection<String> SerialPortNames { get; }
        public ObservableCollection<int> BaudRates { get; }
        public ObservableCollection<Reading> Readings { get; }

        public String WindowTitle { get; }

        public RelayCommand ConnectDisconnectCommand { get; }
        public RelayCommand RefreshPortsCommand { get; }
        public RelayCommand ClearCommand { get; }
        public RelayCommand ExportCommand { get; }

        public String SelectedPortName
        {
            get { return this.selectedPortName; }
            set { SetProperty(ref this.selectedPortName, value); }
        }

        public int SelectedBaudRate
        {
            get { return this.selectedBaudRate; }
            set { SetProperty(ref this.selectedBaudRate, value); }
        }

        public bool IsConnectionSettingsEditable
        {
            get { return this.isConnectionSettingsEditable; }
            private set { SetProperty(ref this.isConnectionSettingsEditable, value); }
        }

        public String ConnectButtonLabel
        {
            get { return this.connectButtonLabel; }
            private set { SetProperty(ref this.connectButtonLabel, value); }
        }

        public String CurrentValue
        {
            get { return this.currentValue; }
            private set { SetProperty(ref this.currentValue, value); }
        }

        public String CurrentTime
        {
            get { return this.currentTime; }
            private set { SetProperty(ref this.currentTime, value); }
        }

        public String StorageIntervalText
        {
            get { return this.storageIntervalText; }
            set
            {
                int newInterval = 0;
                int.TryParse(value, out newInterval);
                if (newInterval > 0)
                {
                    Indicator.Instance.StorageTimerInterval = newInterval;
                }
                RefreshStorageIntervalText();
            }
        }

        private void RefreshStorageIntervalText()
        {
            this.storageIntervalText = Indicator.Instance.StorageTimerInterval.ToString("#0");
            OnPropertyChanged(nameof(StorageIntervalText));
        }

        private void RefreshSerialPortNames()
        {
            this.SerialPortNames.Clear();
            SerialPortUtil.Instance.AvailableSerialPortNames.ForEach(name => this.SerialPortNames.Add(name));
        }

        private void RefreshConnectionDependentState()
        {
            this.IsConnectionSettingsEditable = !Indicator.Instance.IsReady;
            this.ConnectButtonLabel = Indicator.Instance.IsReady ? "Desconectar" : "Conectar";
        }

        private void ExecuteConnectDisconnect()
        {
            if (!Indicator.Instance.IsReady)
            {
                if (this.SelectedPortName == null)
                {
                    this.dialogService.ShowAlert("Selecione uma porta serial.");
                    return;
                }
                try
                {
                    this.isConnecting = true;
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                    Indicator.Instance.Initialize(this.SelectedPortName, this.SelectedBaudRate);
                }
                catch (SerialPortInvalidException)
                {
                    this.dialogService.ShowError("Esta porta serial já não é mais válida.");
                    RefreshSerialPortNames();
                }
                catch (Exception ex)
                {
                    this.dialogService.ShowError(ex.Message);
                }
                finally
                {
                    this.isConnecting = false;
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
            else
            {
                Indicator.Instance.Stop();
            }
        }

        private void ExecuteClear()
        {
            Indicator.Instance.Storage.Clear();
            Indicator.Instance.RestartClock();
        }

        private void ExecuteExport()
        {
            String filePath;
            if (this.dialogService.TryGetSaveCsvPath(out filePath))
            {
                try
                {
                    Indicator.Instance.Storage.ExportToCSV(filePath);
                }
                catch (Exception ex)
                {
                    this.dialogService.ShowError(ex.Message);
                }
            }
        }

        private void OnIndicatorConnectionStateChanged(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(RefreshConnectionDependentState));
        }

        private void OnIndicatorValueChanged(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() => {
                Reading r = Indicator.Instance.Reading;
                this.CurrentValue = r != null ? r.Value : String.Empty;
                this.CurrentTime = r != null ? r.DisplayTime : String.Empty;
            }));
        }

        private void OnStorageChanged(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() => {
                this.Readings.Clear();
                foreach (Reading reading in Indicator.Instance.Storage.Readings)
                {
                    this.Readings.Add(reading);
                }
            }));
        }

        public void OnWindowClosing()
        {
            Indicator.Instance.Terminate();
        }
    }
}
