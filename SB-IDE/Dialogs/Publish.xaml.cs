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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SB_IDE.Dialogs
{
    /// <summary>
    /// Interaction logic for Publish.xaml
    /// </summary>
    public partial class Publish : Window
    {
        private SBInterop sbInterop;
        private string key;

        public Publish(SBInterop sbInterop, string key)
        {
            this.sbInterop = sbInterop;
            this.key = key;
            InitializeComponent();

            FontSize = 12 + MainWindow.zoom;
            label.FontSize = 16 + MainWindow.zoom;

            textBoxPublish.Focus();
            textBoxPublish.Text = key;
            textBoxTitle.Text = "";
            textBoxDescription.Text = "";
            comboBoxCategory.SelectedIndex = 0;
        }

        private void buttonClose_Click(object sender, RoutedEventArgs e)
        {
            ComboBoxItem item = (ComboBoxItem)comboBoxCategory.SelectedItem;
            sbInterop.SetDetails(key, textBoxTitle.Text, textBoxDescription.Text, item.Content.ToString());
            Close();
        }

        private void buttonOpen_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists("C:\\Program Files\\internet explorer\\iexplore.exe"))
            {
                Process.Start("C:\\Program Files\\internet explorer\\iexplore.exe", "http://smallbasic.com/program/?" + key);
            }
        }
    }
}
