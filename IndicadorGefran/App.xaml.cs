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
            System.Windows.MessageBox.Show(message, "Alerta", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public void ShowError(String message)
        {
            System.Windows.MessageBox.Show(message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }

    }
}
