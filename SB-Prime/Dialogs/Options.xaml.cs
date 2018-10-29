using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SB_Prime.Dialogs
{
    /// <summary>
    /// Interaction logic for Options.xaml
    /// </summary>
    public partial class Options : Window
    {
        MainWindow mainWindow;

        public Options(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;

            InitializeComponent();

            FontSize = 12 + MainWindow.zoom;

            textBoxInstallation.Text = MainWindow.InstallDir;
            checkBoxQuoteInserts.IsChecked = MainWindow.quoteInserts;
            checkBoxHEXColors.IsChecked = MainWindow.hexColors;
            checkBoxLoadExtensions.IsChecked = MainWindow.loadExtensions;
        }

        private void buttonUpdates_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://gallery.technet.microsoft.com/Small-Basic-IDE-10-42648328");
        }

        private void buttonUpdate_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.Update();
        }

        private void buttonInstallation_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.ShowNewFolderButton = true;
            fbd.SelectedPath = textBoxInstallation.Text;
            DialogResult result = fbd.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
            {
                textBoxInstallation.Text = fbd.SelectedPath;
            }
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void buttonOK_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.InstallDir != textBoxInstallation.Text)
            {
                if (!Directory.Exists(textBoxInstallation.Text)) textBoxInstallation.Text = "";
                MainWindow.InstallDir = textBoxInstallation.Text;
                mainWindow.sbInterop = new SBInterop();
            }
            MainWindow.quoteInserts = (bool)checkBoxQuoteInserts.IsChecked;
            MainWindow.hexColors = (bool)checkBoxHEXColors.IsChecked;
            MainWindow.loadExtensions = (bool)checkBoxLoadExtensions.IsChecked;

            Close();
        }
    }
}
