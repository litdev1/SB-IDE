using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

            Topmost = MainWindow.topmost;
            FontSize = 12 + MainWindow.zoom;
            label.FontSize = 16 + MainWindow.zoom;

            textBoxImport.Focus();
            textBoxImport.Text = "";

            string data = (string)Clipboard.GetData(DataFormats.Text);
            data = null == data ? "" : data.Trim().ToUpper();
            if (Regex.Match(data, "^[A-Z]{3}[0-9]{3}").Success || Regex.Match(data, "^[A-Z]{4}[0-9]{3}\\.[0-9]{3}").Success)
            {
                textBoxImport.Text = data;
                textBoxImport.CaretIndex = data.Length;
                textBoxImport.SelectAll();
            }
            else if (Regex.Match(data, "^[A-Z]{4}[0-9]{3}").Success)
            {
                data += ".000";
                textBoxImport.Text = data;
                textBoxImport.CaretIndex = data.Length;
                textBoxImport.SelectAll();
            }
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
            string data = textBoxImport.Text.ToUpper();
            if (Regex.Match(data, "^[A-Z]{4}[0-9]{3}").Success && !data.Contains('.'))
            {
                data += ".000";
            }
            MainWindow.ImportProgram = sbInterop.Import(data);
            if (MainWindow.ImportProgram == "error")
            {
                MainWindow.Errors.Add(new Error("Import : " + Properties.Strings.String59 + " " + textBoxImport.Text));
            }
            else
            {
                MainWindow.Errors.Add(new Error("Import : " + Properties.Strings.String60 + " " + textBoxImport.Text));
            }
            Close();
        }
    }
}
