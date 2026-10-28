using System;
using System.Windows;
using Microsoft.Win32;

namespace IndicadorGefran.Services
{
    public class DialogService : IDialogService
    {
        private static DialogService instance;

        private DialogService()
        {
        }

        public static DialogService Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DialogService();
                }
                return instance;
            }
        }

        public void ShowAlert(String message)
        {
            Application.Current.Dispatcher.Invoke(new Action(() => {
                MessageBox.Show(message, "Alerta", MessageBoxButton.OK, MessageBoxImage.Warning);
            }));
        }

        public void ShowError(String message)
        {
            Application.Current.Dispatcher.Invoke(new Action(() => {
                MessageBox.Show(message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }));
        }

        public void ShowInfo(String message)
        {
            Application.Current.Dispatcher.Invoke(new Action(() => {
                MessageBox.Show(message, "Informação", MessageBoxButton.OK, MessageBoxImage.Information);
            }));
        }

        public bool TryGetSaveCsvPath(out String filePath)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Comma-separated Values (*.csv)|*.csv";
            if (dialog.ShowDialog() == true)
            {
                filePath = dialog.FileName;
                return true;
            }
            filePath = null;
            return false;
        }
    }
}
