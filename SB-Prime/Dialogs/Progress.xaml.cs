using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SB_Prime.Dialogs
{
    /// <summary>
    /// Interaction logic for Progress.xaml
    /// </summary>
    public partial class Progress : Window
    {
        private Timer timer;

        public Progress()
        {
            InitializeComponent();

            FontSize = 12 + MainWindow.zoom;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            timer = new Timer(new TimerCallback(_timer), null, 10, 10);
        }

        private void _timer(object state)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    textBoxDir.Text = FileSearcher.ProgressDir;
                    textBoxFound.Text = FileSearcher.ProgressCount + " files found";
                    textBoxFailed.Text = FileSearcher.ProgressFailed + " folders or files could not be processed";
                    if (FileSearcher.ProgressState != 1) Close();
                });
            }
            catch
            {
                Dispatcher.Invoke(() =>
                {
                    Close();
                });
            }
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            FileSearcher.ProgressState = 2;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            timer.Dispose();
            FileSearcher.ProgressState = 2;
        }
    }
}
