using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    /// Interaction logic for Import.xaml
    /// </summary>
    public partial class Import : Window
    {
        SBInterop sbInterop;

        internal Import(SBInterop sbInterop)
        {
            this.sbInterop = sbInterop;

            InitializeComponent();

            FontSize = 12 + MainWindow.zoom;
            label.FontSize = 16 + MainWindow.zoom;

            textBoxImport.Focus();
            textBoxImport.Text = "";
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.ImportProgram = "";
            Close();
        }

        private void buttonOK_Click(object sender, RoutedEventArgs e)
        {
            OK();
        }

        private void textBoxImport_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                OK();
            }
        }

        private void OK()
        {
            MainWindow.ImportProgram = sbInterop.Import(textBoxImport.Text);
            if (MainWindow.ImportProgram == "error")
            {
                MainWindow.Errors.Add(new Error("Import : " + "Failed to import program with ID " + textBoxImport.Text));
            }
            else
            {
                MainWindow.Errors.Add(new Error("Import : " + "Successfully imported program with ID " + textBoxImport.Text));
            }
            Close();
        }
    }
}
