using System;
using System.Configuration;
using System.Windows;
using IndicadorGefran.Model;

namespace IndicadorGefran
{
    /// <summary>
    /// Interação lógica para App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            bool debugModeEnabled;
            bool.TryParse(ConfigurationManager.AppSettings["DebugModeEnabled"], out debugModeEnabled);
            AppLogger.Instance.Enabled = debugModeEnabled;
            AppLogger.Instance.Info("Aplicacao iniciada (versao " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version + ")");

            // Apenas registram; nao marcam a excecao como tratada, o comportamento padrao continua igual.
            this.DispatcherUnhandledException += (s, args) => AppLogger.Instance.Error("Excecao nao tratada na UI thread", args.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, args) => AppLogger.Instance.Error("Excecao nao tratada (AppDomain)", args.ExceptionObject as Exception);

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppLogger.Instance.Info("Aplicacao encerrada");
            AppLogger.Instance.Enabled = false;
            base.OnExit(e);
        }
    }
}
