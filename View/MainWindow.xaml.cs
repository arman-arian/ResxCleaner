using System;
using ResxCleaner.Properties;
using ResxCleaner.ViewModel;

namespace ResxCleaner.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.DataContext = new MainViewModel();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Settings.Default.MainWindowPlacement = this.GetPlacement();

            var vm = this.DataContext as MainViewModel;
            vm?.OnClose();

            Settings.Default.KeyColumnWidth = this.keyColumn.ActualWidth;
            Settings.Default.ValueColumnWidth = this.valueColumn.ActualWidth;
            Settings.Default.Save();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            this.SetPlacement(Settings.Default.MainWindowPlacement);

            this.keyColumn.Width = Settings.Default.KeyColumnWidth;
            this.valueColumn.Width = Settings.Default.ValueColumnWidth;
        }
    }
}
