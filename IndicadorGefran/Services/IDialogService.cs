using System;

namespace IndicadorGefran.Services
{
    public interface IDialogService
    {
        void ShowAlert(String message);
        void ShowError(String message);
        void ShowInfo(String message);
        bool TryGetSaveCsvPath(out String filePath);
    }
}
