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
    /// Interaction logic for Colours.xaml
    /// </summary>
    public partial class Colours : Window
    {
        MainWindow mainWindow;
        private List<ColourData> colours = new List<ColourData>();

        public Colours(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;

            InitializeComponent();

            FontSize = 12 + MainWindow.zoom;

            dataGridColours.ItemsSource = colours;

            var ideColors = mainWindow.IDEColors;
            foreach (KeyValuePair<string,int> kvp in ideColors)
            {
                string[] data = kvp.Key.Split(':');
                string region = "Unset";
                if (data[0] == "W") region = "Main Window";
                else if (data[0] == "D") region = "Document Layout";
                else if (data[0] == "L") region = "Document Lexer";
                colours.Add(new ColourData() { Region = region, Label = data[1], R = (byte)(kvp.Value >> 16), G = (byte)(kvp.Value >> 8), B = (byte)(kvp.Value) });
            }
        }

        private void dataGridColours_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            dataGridColours.Items.Refresh();
        }

        private void dataGridColoursSet(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            try
            {
                ColourData data = (ColourData)button.Tag;
                if (null != data)
                {
                    System.Windows.Forms.ColorDialog cd = new System.Windows.Forms.ColorDialog();
                    cd.Color = System.Drawing.Color.FromArgb(255, data.R, data.G, data.B);
                    cd.AnyColor = true;
                    cd.SolidColorOnly = true;
                    cd.FullOpen = true;
                    if (cd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        data.R = cd.Color.R;
                        data.G = cd.Color.G;
                        data.B = cd.Color.B;
                        button.Background = new SolidColorBrush(Color.FromRgb(data.R, data.G, data.B));
                        dataGridColours.Items.Refresh();
                    }
                }
            }
            catch
            {

            }
        }

        private void Button_Loaded(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            try
            {
                ColourData data = (ColourData)button.Tag;
                if (null != data) button.Background = new SolidColorBrush(Color.FromRgb(data.R, data.G, data.B));
            }
            catch
            {

            }
        }

        private void buttonDefaults_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < colours.Count; i++)
            {
                colours[i].R = (byte)(mainWindow.DefaultColors.ElementAt(i).Value >> 16);
                colours[i].G = (byte)(mainWindow.DefaultColors.ElementAt(i).Value >> 8);
                colours[i].B = (byte)(mainWindow.DefaultColors.ElementAt(i).Value);
            }
            dataGridColours.Items.Refresh();
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            var ideColors = mainWindow.IDEColors;
            for (int i = 0; i < colours.Count; i++)
            {
                ideColors[ideColors.Keys.ElementAt(i)] = (colours[i].R << 16) | (colours[i].G << 8) | colours[i].B;
            }
            mainWindow.IDEColors = ideColors;
            Close();
        }

        private void dataGridColours_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                try
                {
                    string data = (string)Clipboard.GetData(DataFormats.Text);
                    string[] rows = data.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (rows.Length == colours.Count)
                    {
                        for (int i = 0; i < rows.Length; i++)
                        {
                            string row = rows[i];
                            string[] cols = row.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (cols.Length >= 3)
                            {
                                byte R = 0, G = 0, B = 0;
                                if (byte.TryParse(cols[cols.Length - 3], out R) && byte.TryParse(cols[cols.Length - 2], out G) && byte.TryParse(cols[cols.Length - 1], out B))
                                {
                                    colours[i].R = R;
                                    colours[i].G = G;
                                    colours[i].B = B;
                                }
                            }
                        }
                    }
                    dataGridColours.Items.Refresh();
                }
                catch
                {

                }
            }
        }
    }

    public class ColourData
    {
        public string Region { get; set; }
        public string Label { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
    }
}
