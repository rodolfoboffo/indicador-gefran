using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace IndicadorGefran
{
    /// <summary>
    /// Interação lógica para App.xaml
    /// </summary>
    public partial class App : Application
    {

        public void ShowAlert(String message)
        {
            Application.Current.Dispatcher.Invoke(new Action(() => {
                System.Windows.MessageBox.Show(message, "Alerta", MessageBoxButton.OK, MessageBoxImage.Warning);
            }));
        }

        public void ShowError(String message)
        {
            Application.Current.Dispatcher.Invoke(new Action(() => {
                System.Windows.MessageBox.Show(message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }));
        }

        public void ShowInfo(String message)
        {
            Application.Current.Dispatcher.Invoke(new Action(() => {
                System.Windows.MessageBox.Show(message, "Informação", MessageBoxButton.OK, MessageBoxImage.Information);
            }));
        }

    }
}
