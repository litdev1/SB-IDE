﻿using ScintillaNET;
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

namespace SB_IDE.Dialogs
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

            Topmost = true;
            FontSize = 12 + MainWindow.zoom;

            switch (mode)
            {
                case 0:
                    SetColors();
                    break;
                case 1:
                    SetFonts();
                    break;
            }

            listViewPopup.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Left = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width - listViewPopup.DesiredSize.Width - 20;
            Top = (System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height - listViewPopup.DesiredSize.Height) * (1 + mode) / 3;
        }

        private void listViewPopup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListView listView = (ListView)sender;
            Grid grid = (Grid)listView.SelectedItem;
            TextBlock tb = (TextBlock)grid.Children[0];
            Scintilla textArea = mainWindow.GetActiveDocument().TextArea;
            textArea.ReplaceSelection("\"" + tb.Text + "\"");
            textArea.SelectionStart = textArea.CurrentPosition;
            textArea.SelectionEnd = textArea.CurrentPosition;
        }

        private void SetColors()
        {
            Title = "Insert Color Name";
            Type colorsType = typeof(System.Windows.Media.Colors);
            PropertyInfo[] colorsTypePropertyInfos = colorsType.GetProperties(BindingFlags.Public | BindingFlags.Static);

            foreach (PropertyInfo colorsTypePropertyInfo in colorsTypePropertyInfos)
            {
                string colorName = colorsTypePropertyInfo.Name;

                Grid grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition() { });
                grid.RowDefinitions.Add(new RowDefinition() { });

                TextBlock tb = new TextBlock() { Text = colorName, HorizontalAlignment = HorizontalAlignment.Center };
                Rectangle color = new Rectangle() { Height = 20, Width = 100, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorName)) };
                grid.Children.Add(tb);
                grid.Children.Add(color);
                Grid.SetRow(tb, 0);
                Grid.SetRow(color, 1);
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

                listViewPopup.Items.Add(grid);
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Topmost = true;
            Activate();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (PopupList popup in popups)
            {
                if (mode == popup.mode)
                {
                    popup.WindowState = WindowState.Normal;
                    popup.Topmost = true;
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