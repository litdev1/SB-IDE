using ScintillaNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SB_Prime.Dialogs
{
    /// <summary>
    /// Interaction logic for PopupList.xaml
    /// </summary>
    public partial class PopupList : Window
    {
        private MainWindow mainWindow;
        private int mode;
        private static List<PopupList> popups = new List<PopupList>();

        public PopupList(MainWindow mainWindow, int mode)
        {
            this.mainWindow = mainWindow;
            this.mode = mode;

            InitializeComponent();

            Topmost = MainWindow.topmost;
            FontSize = 12 + MainWindow.zoom;

            FrameworkElementFactory fef = new FrameworkElementFactory(typeof(UniformGrid));
            listViewPopup.ItemsPanel = new ItemsPanelTemplate(fef);

            switch (mode)
            {
                case 0:
                    fef.SetValue(UniformGrid.ColumnsProperty, 4);
                    SetColors();
                    break;
                case 1:
                    fef.SetValue(UniformGrid.ColumnsProperty, 3);
                    SetFonts();
                    break;
                case 2:
                    fef.SetValue(UniformGrid.ColumnsProperty, 16);
                    SetCharacters();
                    break;
            }

            listViewPopup.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Left = SystemParameters.PrimaryScreenWidth - listViewPopup.DesiredSize.Width - 20;
            Top = (SystemParameters.PrimaryScreenHeight - listViewPopup.DesiredSize.Height) * (1 + mode) / 6;
        }

        private void listViewPopup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (mode == 0 || mode == 1)
            {
                ListView listView = (ListView)sender;
                Grid grid = (Grid)listView.SelectedItem;
                TextBlock tb = (TextBlock)grid.Children[0];
                string value = MainWindow.hexColors && null != tb.Tag ? tb.Tag.ToString() : tb.Text;
                Scintilla textArea = mainWindow.GetActiveDocument().TextArea;
                if (MainWindow.quoteInserts)
                    textArea.ReplaceSelection("\"" + value + "\"");
                else
                    textArea.ReplaceSelection(value);
                textArea.SelectionStart = textArea.CurrentPosition;
                textArea.SelectionEnd = textArea.CurrentPosition;
            }
            else if (mode == 2)
            {
                ListView listView = (ListView)sender;
                Grid grid = (Grid)listView.SelectedItem;
                TextBlock tb = (TextBlock)grid.Children[0];
                string value = char.ConvertFromUtf32((int)grid.Tag);
                Scintilla textArea = mainWindow.GetActiveDocument().TextArea;
                textArea.ReplaceSelection(value);
                textArea.SelectionStart = textArea.CurrentPosition;
                textArea.SelectionEnd = textArea.CurrentPosition;
            }
        }

        private void SetColors()
        {
            Title = "Insert Color Name";
            Type colorsType = typeof(Colors);
            PropertyInfo[] colorsTypePropertyInfos = colorsType.GetProperties(BindingFlags.Public | BindingFlags.Static);

            foreach (PropertyInfo colorsTypePropertyInfo in colorsTypePropertyInfos)
            {
                string colorName = colorsTypePropertyInfo.Name;

                Grid grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition() { });
                grid.RowDefinitions.Add(new RowDefinition() { });
                grid.RowDefinitions.Add(new RowDefinition() { });

                Color col = (Color)ColorConverter.ConvertFromString(colorName);
                TextBlock tb1 = new TextBlock() { Text = colorName, HorizontalAlignment = HorizontalAlignment.Center };
                tb1.Tag = col.ToString();
                TextBlock tb2 = new TextBlock() { Text = col.ToString(), HorizontalAlignment = HorizontalAlignment.Center };
                Rectangle color = new Rectangle() { Height = 20, Width = 100, Fill = new SolidColorBrush(col) };
                grid.Children.Add(tb1);
                grid.Children.Add(tb2);
                grid.Children.Add(color);
                Grid.SetRow(tb1, 0);
                Grid.SetRow(tb2, 1);
                Grid.SetRow(color, 2);
                grid.Tag = System.Drawing.Color.FromName(colorName).GetHue();

                listViewPopup.Items.Add(grid);
            }

            listViewPopup.Items.SortDescriptions.Add(
                new System.ComponentModel.SortDescription("Tag",
                System.ComponentModel.ListSortDirection.Ascending));
        }

        private void SetFonts()
        {
            Title = "Insert Font Name";

            foreach (FontFamily font in Fonts.SystemFontFamilies)
            {
                try
                {
                    string fontName = font.FamilyNames.Values.First();
                    double fontSize = FontSize;

                    Grid grid = new Grid();
                    grid.RowDefinitions.Add(new RowDefinition() { });
                    grid.RowDefinitions.Add(new RowDefinition() { });

                    TextBlock tb = new TextBlock() { Text = fontName, HorizontalAlignment = HorizontalAlignment.Center };
                    TextBlock text = new TextBlock() { Text = "Small Basic", FontFamily = font, FontSize = fontSize + 4, HorizontalAlignment = HorizontalAlignment.Center };
                    grid.Children.Add(tb);
                    grid.Children.Add(text);
                    Grid.SetRow(tb, 0);
                    Grid.SetRow(text, 1);
                    grid.Tag = fontName;

                    listViewPopup.Items.Add(grid);
                }
                catch
                {

                }
            }

            listViewPopup.Items.SortDescriptions.Add(
                new System.ComponentModel.SortDescription("Tag",
                System.ComponentModel.ListSortDirection.Ascending));
        }

        private void SetCharacters()
        {
            Title = "Insert Character";

            for (int i = 0; i < 10000; i++)
            {
                try
                {
                    Grid grid = new Grid();
                    //grid.ColumnDefinitions.Add(new ColumnDefinition() { });
                    //grid.ColumnDefinitions.Add(new ColumnDefinition() { });

                    //TextBlock tb = new TextBlock() { Text = i.ToString(), HorizontalAlignment = HorizontalAlignment.Center };
                    string value = char.ConvertFromUtf32(i);
                    //value += char.GetUnicodeCategory(value, 0);
                    TextBlock text = new TextBlock() { Text = value, FontWeight=FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
                    //grid.Children.Add(tb);
                    grid.Children.Add(text);
                    grid.Tag = i;
                    //Grid.SetColumn(tb, 0);
                    //Grid.SetColumn(text, 1);

                    listViewPopup.Items.Add(grid);
                }
                catch
                {

                }
            }

            listViewPopup.Items.SortDescriptions.Add(
                new System.ComponentModel.SortDescription("Tag",
                System.ComponentModel.ListSortDirection.Ascending));
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Topmost = MainWindow.topmost;
            Activate();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (PopupList popup in popups)
            {
                if (mode == popup.mode)
                {
                    popup.WindowState = WindowState.Normal;
                    popup.Topmost = MainWindow.topmost;
                    popup.Activate();
                    Close();
                    return;
                }
            }
            popups.Add(this);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            foreach (PopupList popup in popups)
            {
                if (this == popup)
                {
                    popups.Remove(popup);
                    break;
                }
            }
        }
    }
}
