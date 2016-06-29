using System.Runtime.InteropServices;
using System.Windows;

namespace ResxCleaner.Services
{
    public static class ClipboardService
    {
        public static bool SetText(string text)
        {
            for (var i = 0; i < 5; i++)
            {
                try
                {
                    Clipboard.SetText(text);
                    return true;
                }
                catch (COMException)
                {
                    // retry
                }
            }

            MessageBox.Show("Unable to copy text. Please try again later.");
            return false;
        }
    }
}
