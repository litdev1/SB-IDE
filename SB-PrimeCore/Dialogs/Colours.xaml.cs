using System;
using System.Collections.Generic;
using System.Data;
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
    /// Interaction logic for Colours.xaml
    /// </summary>
    public partial class Colours : Window
    {
        MainWindow mainWindow;
        private List<ColourData> colours = new List<ColourData>();
        private string[] labels = new string[]
        {
                Properties.Strings.String129,
                Properties.Strings.String130,
                Properties.Strings.String131,
                Properties.Strings.String132,
                Properties.Strings.String133,
                Properties.Strings.String134,
                Properties.Strings.String135,
                Properties.Strings.String136,
                Properties.Strings.String137,
                Properties.Strings.String138,
                Properties.Strings.String139,
                Properties.Strings.String140,
                Properties.Strings.String141,
                Properties.Strings.String142,
                Properties.Strings.String143,
                Properties.Strings.String144,
                Properties.Strings.String145,
                Properties.Strings.String146,
                Properties.Strings.String147,
                Properties.Strings.String148,
                Properties.Strings.String149,
                Properties.Strings.String150,
                Properties.Strings.String151,
                Properties.Strings.String152,
                Properties.Strings.String153,
                Properties.Strings.String154,
                Properties.Strings.String155,
                Properties.Strings.String156,
                Properties.Strings.String157,
                Properties.Strings.String158,
                Properties.Strings.String159,
                Properties.Strings.String160,
                Properties.Strings.String161,
                Properties.Strings.String162,
                Properties.Strings.String163,
                Properties.Strings.String164,
                Properties.Strings.String165,
                Properties.Strings.String166,
                Properties.Strings.String167,
        };

        public Colours(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;

            InitializeComponent();

            Topmost = MainWindow.topmost;
            FontSize = 12 + MainWindow.zoom;

            dataGridColours.ItemsSource = colours;

            var ideColors = mainWindow.IDEColors;
            int i = 0;
            foreach (KeyValuePair<string,int> kvp in ideColors)
            {
                string[] data = kvp.Key.Split(':');
                string region = Properties.Strings.String124;
                if (data[0] == "W") region = Properties.Strings.String125;
                else if (data[0] == "D") region = Properties.Strings.String126;
                else if (data[0] == "L") region = Properties.Strings.String127;
                else if (data[0] == "C") region = Properties.Strings.String128;
                byte R = (byte)(kvp.Value >> 16);
                byte G = (byte)(kvp.Value >> 8);
                byte B = (byte)(kvp.Value);
                colours.Add(new ColourData() { Region = region, Label = labels[i++], R = R, G = G, B = B, Color = new SolidColorBrush(Color.FromRgb(R, G, B)) });
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
                        data.Color = new SolidColorBrush(Color.FromRgb(data.R, data.G, data.B));
                        dataGridColours.Items.Refresh();
                    }
                }
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
                colours[i].Color = new SolidColorBrush(Color.FromRgb(colours[i].R, colours[i].G, colours[i].B));
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
                    int columns = data.Count(f => f == '\t') / colours.Count;
                    if (rows.Length == colours.Count && columns >= 2)
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
                                    colours[i].Color = new SolidColorBrush(Color.FromRgb(colours[i].R, colours[i].G, colours[i].B));
                                }
                            }
                        }
                    }
                    else if (dataGridColours.SelectedCells.Count == 1)
                    {
                        int iCol = 0;
                        if (dataGridColours.SelectedCells[0].Column.Header.ToString() == "Green") iCol = 1;
                        else if (dataGridColours.SelectedCells[0].Column.Header.ToString() == "Blue") iCol = 2;

                        int iRow = 0;
                        for (iRow = 0; iRow < colours.Count; iRow++)
                        {
                            if (dataGridColours.Items[iRow] == dataGridColours.SelectedCells[0].Item) break;
                        }

                        for (int i = 0; i < rows.Length; i++)
                        {
                            if (iRow + i >= colours.Count) break;
                            string[] cols = rows[i].Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int j = 0; j < cols.Length; j++)
                            {
                                byte C = 0;
                                if (byte.TryParse(cols[j], out C))
                                {
                                    if (iCol + j == 0) colours[iRow + i].R = C;
                                    else if (iCol + j == 1) colours[iRow + i].G = C;
                                    else if (iCol + j == 2) colours[iRow + i].B = C;
                                }
                            }
                            colours[iRow + i].Color = new SolidColorBrush(Color.FromRgb(colours[iRow + i].R, colours[iRow + i].G, colours[iRow + i].B));
                        }
                    }
                    dataGridColours.Items.Refresh();
                }
                catch
                {

                }
            }
        }

        private void dataGridColours_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            int iRow = 0;
            for (iRow = 0; iRow < colours.Count; iRow++)
            {
                if (dataGridColours.Items[iRow] == e.Row.Item) break;
            }

            byte value = 0;
            TextBox tb = (TextBox)e.EditingElement;
            if (!byte.TryParse(tb.Text, out value))
            {
                e.Cancel = true;
                return;
            }

            if (e.Column.Header.ToString() == "Red")
            {
                colours[iRow].Color = new SolidColorBrush(Color.FromRgb(value, colours[iRow].G, colours[iRow].B));
            }
            else if (e.Column.Header.ToString() == "Green")
            {
                colours[iRow].Color = new SolidColorBrush(Color.FromRgb(colours[iRow].R, value, colours[iRow].B));
            }
            else if (e.Column.Header.ToString() == "Blue")
            {
                colours[iRow].Color = new SolidColorBrush(Color.FromRgb(colours[iRow].R, colours[iRow].G, value));
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
        public Brush Color { get; set; }
    }
}
