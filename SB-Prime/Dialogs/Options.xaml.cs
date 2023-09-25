﻿using System;
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
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static ScintillaPrinting.PageSettings;

namespace SB_Prime.Dialogs
{
    /// <summary>
    /// Interaction logic for Options.xaml
    /// </summary>
    public partial class Options : Window
    {
        private MainWindow mainWindow;

        public Options(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;

            InitializeComponent();

            FontSize = 12 + MainWindow.zoom;

            textBoxInstallation.Text = MainWindow.InstallDir;
            checkBoxQuoteInserts.IsChecked = MainWindow.quoteInserts;
            checkBoxHEXColors.IsChecked = MainWindow.hexColors;
            checkBoxLoadExtensions.IsChecked = MainWindow.loadExtensions;
            checkBoxTopmost.IsChecked = MainWindow.topmost;
            textBoxPrintMagnification.Text = MainWindow.printMagnification.ToString();
            textBoxRegex.Text = MainWindow.exRegex;
            comboBoxPrintColours.Items.Clear();
            comboBoxPrintColours.Items.Add(new TextBlock() { Text = Properties.Strings.String114, Tag = (int)PrintColorMode.ColorOnWhite });
            comboBoxPrintColours.Items.Add(new TextBlock() { Text = Properties.Strings.String115, Tag = (int)PrintColorMode.Normal });
            comboBoxPrintColours.Items.Add(new TextBlock() { Text = Properties.Strings.String116, Tag = (int)PrintColorMode.BlackOnWhite });
            comboBoxPrintColours.SelectedItem = comboBoxPrintColours.Items.OfType<TextBlock>().SingleOrDefault(x => (int)x.Tag == MainWindow.printColours);
            if (MainWindow.indentSpaces == 1) radioButton1.IsChecked = true;
            else if (MainWindow.indentSpaces == 4) radioButton4.IsChecked = true;
            else radioButton2.IsChecked = true;
            List<TextBlock> fonts = new List<TextBlock>();
            foreach (FontFamily font in Fonts.SystemFontFamilies) //WPF fonts
            {
                string fontName = font.FamilyNames.Values.First();
                double fontSize = FontSize;

                TextBlock text = new TextBlock() { Text = fontName, FontFamily = font, FontSize = fontSize, HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
                text.Tag = fontName;
                fonts.Add(text);
            }
            fonts.Sort(SortCompareFont);
            comboBoxLexerFont.Items.Clear();
            foreach (TextBlock text in fonts)
            {
                comboBoxLexerFont.Items.Add(text);
            }
            comboBoxLexerFont.SelectedItem = comboBoxLexerFont.Items.OfType<TextBlock>().SingleOrDefault(x => (string)x.Tag == MainWindow.lexerFont);
        }

        private int SortCompareFont(TextBlock x, TextBlock y)
        {
            return x.Tag.ToString().CompareTo(y.Tag.ToString());
        }

        private void buttonUpdate_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.Update();
        }

        private void buttonInstallation_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.ShowNewFolderButton = true;
            fbd.SelectedPath = textBoxInstallation.Text;
            DialogResult result = fbd.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
            {
                textBoxInstallation.Text = fbd.SelectedPath;
            }
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void buttonOK_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.InstallDir != textBoxInstallation.Text)
            {
                if (!Directory.Exists(textBoxInstallation.Text)) textBoxInstallation.Text = "";
                MainWindow.InstallDir = textBoxInstallation.Text;
                mainWindow.sbInterop = new SBInterop();
            }
            MainWindow.quoteInserts = (bool)checkBoxQuoteInserts.IsChecked;
            MainWindow.hexColors = (bool)checkBoxHEXColors.IsChecked;
            MainWindow.loadExtensions = (bool)checkBoxLoadExtensions.IsChecked;
            MainWindow.topmost = (bool)checkBoxTopmost.IsChecked;
            short.TryParse(textBoxPrintMagnification.Text, out MainWindow.printMagnification);
            MainWindow.exRegex = textBoxRegex.Text;
            MainWindow.printColours = (int)((TextBlock)comboBoxPrintColours.SelectedItem).Tag;
            if (radioButton1.IsChecked == true) MainWindow.indentSpaces = 1;
            else if (radioButton4.IsChecked == true) MainWindow.indentSpaces = 4;
            else MainWindow.indentSpaces = 2;
            MainWindow.lexerFont = ((TextBlock)(comboBoxLexerFont.SelectedItem)).Tag.ToString();
            Close();
        }
    }
}
