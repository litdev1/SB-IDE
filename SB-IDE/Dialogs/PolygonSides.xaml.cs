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

namespace SB_IDE.Dialogs
{
    /// <summary>
    /// Interaction logic for PolygonSides.xaml
    /// </summary>
    public partial class PolygonSides : Window
    {
        public static int NumSides = 5;

        public PolygonSides()
        {
            InitializeComponent();

            FontSize = 12 + MainWindow.zoom;

            textBoxNumSides.Text = NumSides.ToString();
        }

        private void buttonDone_Click(object sender, RoutedEventArgs e)
        {
            int.TryParse(textBoxNumSides.Text, out NumSides);
            Close();
        }
    }
}
