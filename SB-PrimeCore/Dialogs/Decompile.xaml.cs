using System;
using System.Collections.Generic;
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
//using System.Windows.Shapes;

namespace SB_Prime.Dialogs
{
    /// <summary>
    /// Interaction logic for Decompile.xaml
    /// </summary>
    public partial class Decompile : Window
    {
        private MainWindow mainWindow;
        public static string sourceFile = "";
        public static string targetFolder = "";
        public static bool bConsole = true;

        public Decompile(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;

            InitializeComponent();

            Topmost = MainWindow.topmost;
            FontSize = 12 + MainWindow.zoom;
            label.FontSize = 16 + MainWindow.zoom;

            textBoxDecompileSource.Text = sourceFile;
            textBoxDecompileFolder.Text = targetFolder;
            textBoxDecompileFolder.Focus();
            buttonDecompileContinue.IsEnabled = File.Exists(textBoxDecompileSource.Text) && Directory.Exists(textBoxDecompileFolder.Text);
            checkBoxConsole.IsChecked = bConsole;
        }

        private void buttonDecompileFolder_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.ShowNewFolderButton = true;
            fbd.SelectedPath = targetFolder;
            if (fbd.SelectedPath == "") fbd.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            DialogResult result = fbd.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
            {
                textBoxDecompileFolder.Text = fbd.SelectedPath;
                buttonDecompileContinue.IsEnabled = File.Exists(textBoxDecompileSource.Text);
            }
        }

        private void buttonDecompileCancel_Click(object sender, RoutedEventArgs e)
        {
            sourceFile = textBoxDecompileSource.Text;
            targetFolder = textBoxDecompileFolder.Text;
            bConsole = (bool)checkBoxConsole.IsChecked;
            Close();
        }

        private void buttonDecompileContinue_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Input.Cursor cursor = Mouse.OverrideCursor;
            if (null == cursor) cursor = System.Windows.Input.Cursors.Arrow;
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            try
            {
                sourceFile = textBoxDecompileSource.Text;
                targetFolder = textBoxDecompileFolder.Text;
                if (!Directory.Exists(targetFolder) || !File.Exists(sourceFile)) return;
                if (Directory.GetFiles(targetFolder).Length > 0 || Directory.GetDirectories(targetFolder).Length > 0)
                {
                    System.Windows.MessageBox.Show(Properties.Strings.String22 + "\n\n" + Properties.Strings.String23, "SB-Prime", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                string targetProg = textBoxDecompileFolder.Text + "\\" + Path.GetFileNameWithoutExtension(sourceFile) + ".csproj";
                bConsole = (bool)checkBoxConsole.IsChecked;

                if (mainWindow.sbInterop.Decomple(targetProg, sourceFile, bConsole))
                {
                    Close();
                }
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Decomple : " + ex.Message));
            }

            Mouse.OverrideCursor = cursor;
        }

        private void textBoxDecompileFolder_TextChanged(object sender, TextChangedEventArgs e)
        {
            buttonDecompileContinue.IsEnabled = File.Exists(textBoxDecompileSource.Text) && Directory.Exists(textBoxDecompileFolder.Text);
        }

        private void textBoxDecompileSource_TextChanged(object sender, TextChangedEventArgs e)
        {
            buttonDecompileContinue.IsEnabled = File.Exists(textBoxDecompileSource.Text) && Directory.Exists(textBoxDecompileFolder.Text);
        }

        private void buttonDecompileSource_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = ".Net Assemblies (*.exe)|*.exe|(*.dll)|*.dll|All files (*.*)|*.*";
            ofd.FilterIndex = 1;
            ofd.RestoreDirectory = true;
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                textBoxDecompileSource.Text = ofd.FileName;
                buttonDecompileContinue.IsEnabled = Directory.Exists(textBoxDecompileFolder.Text);
            }
        }
    }
}
