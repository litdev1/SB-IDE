using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private string key;

        public Publish(string key)
        {
            this.key = key;
            InitializeComponent();

            FontSize = 12 + MainWindow.zoom;
            label.FontSize = 16 + MainWindow.zoom;

            textBoxPublish.Focus();
            textBoxPublish.Text = key;
        }

        private void buttonClose_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("C:\\Program Files\\internet explorer\\iexplore.exe", "http://smallbasic.com/program/?" + key);
            Close();
        }
    }
}
