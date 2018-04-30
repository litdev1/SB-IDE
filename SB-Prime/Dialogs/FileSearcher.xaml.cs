﻿using ScintillaNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SB_Prime.Dialogs
{
    /// <summary>
    /// Interaction logic for FileSearcher.xaml
    /// </summary>
    public partial class FileSearcher : Window
    {
        private List<SearchFile> searchFiles = new List<SearchFile>();
        public static string RootPath = "";
        public static bool Active = false;

        public FileSearcher()
        {
            InitializeComponent();

            FontSize = 12 + MainWindow.zoom;

            Topmost = true;
            textBoxSearcherRoot.Text = RootPath;
            checkBoxSearcherWord.IsChecked = true;

            Left = SystemParameters.PrimaryScreenWidth - Width - 20;
            Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;
        }

        private void buttonSearcherBrowse_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.ShowNewFolderButton = true;
            fbd.SelectedPath = RootPath;
            DialogResult result = fbd.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
            {
                RootPath = fbd.SelectedPath;
                textBoxSearcherRoot.Text = fbd.SelectedPath;
                GetFiles();
            }
        }

        private void buttonSearcherFilter_Click(object sender, RoutedEventArgs e)
        {
            Filter();
        }

        private void buttonSearcherOpen_Click(object sender, RoutedEventArgs e)
        {
            SearchFile searchFile = (SearchFile)dataGridSearcher.SelectedItem;
            if (null != searchFile) MainWindow.MarkedForOpen.Enqueue(searchFile.FilePath);
        }

        private void dataGridSearcher_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SearchFile searchFile = (SearchFile)dataGridSearcher.SelectedItem;
            if (null != searchFile) DisplayFile(searchFile);
        }

        private void GetFiles()
        {
            if (!Directory.Exists(RootPath)) return;
            System.Windows.Input.Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            Stack<string> dirs = new Stack<string>();
            List<string> files = new List<string>();
            dirs.Push(RootPath);

            while (dirs.Count > 0)
            {
                string dir = dirs.Pop();
                string[] _dirs = Directory.GetDirectories(dir);
                foreach (string _dir in _dirs)
                {
                    dirs.Push(_dir);
                }
                string[] _files = Directory.GetFiles(dir);
                foreach (string _file in _files)
                {
                    if (_file.EndsWith(".sb") || _file.EndsWith(".smallbasic")) files.Add(_file);
                }
            }
            searchFiles.Clear();
            foreach (string file in files)
            {
                searchFiles.Add(new SearchFile(file));
            }
            searchFiles.Sort();
            dataGridSearcher.ItemsSource = searchFiles;
            textBoxCount.Text = searchFiles.Count+" files found";

            Mouse.OverrideCursor = cursor;
        }

        private void Filter()
        {
            if (!Directory.Exists(RootPath)) return;

            GetFiles();

            string keyword = textBoxSearcherText.Text;
            List<SearchFile> filterFiles = new List<SearchFile>();
            RegexOptions caseSensitive = RegexOptions.IgnoreCase;
            string wholeWord = (bool)checkBoxSearcherWord.IsChecked ? "\\b" + keyword + "\\b" : keyword;
            foreach (SearchFile file in searchFiles)
            {
                bool isFound = Regex.IsMatch(string.Concat(file.Text), wholeWord, caseSensitive);
                if (keyword == "" || file.FilePath.ToLower().Contains(keyword.ToLower()) || isFound) filterFiles.Add(file);
            }
            dataGridSearcher.ItemsSource = filterFiles;
            textBoxCount.Text = filterFiles.Count + " files found";
        }

        private void DisplayFile(SearchFile searchFile)
        {
            WindowsFormsHost host = new WindowsFormsHost();
            SBDocument doc = new SBDocument();
            host.Child = doc.TextArea;
            doc.TextArea.Text = File.ReadAllText(searchFile.FilePath);
            gridSearcherPreview.Children.Add(host);
            doc.WrapMode = MainWindow.wrap ? WrapMode.Whitespace : WrapMode.None;
            doc.IndentationGuides = MainWindow.indent ? IndentView.LookBoth : IndentView.None;
            doc.ViewWhitespace = MainWindow.whitespace ? WhitespaceMode.VisibleAlways : WhitespaceMode.Invisible;
            doc.TextArea.Zoom = MainWindow.zoom;
            doc.Theme = MainWindow.theme;

            doc.TextArea.ReadOnly = true;
            doc.TextArea.SearchFlags = SearchFlags.None;
            doc.TextArea.Indicators[0].ForeColor = SBDocument.IntToColor(MainWindow.FIND_HIGHLIGHT_COLOR);
            doc.TextArea.Indicators[0].Style = IndicatorStyle.RoundBox;
            if (textBoxSearcherText.Text != "")
            {
                doc.TextArea.TargetStart = 0;
                doc.TextArea.TargetEnd = doc.TextArea.TextLength;

                string keyword = textBoxSearcherText.Text;
                RegexOptions caseSensitive = RegexOptions.IgnoreCase;
                string wholeWord = (bool)checkBoxSearcherWord.IsChecked ? "\\b" + keyword + "\\b" : keyword;
                MatchCollection matches = Regex.Matches(doc.TextArea.Text, wholeWord, caseSensitive);
                foreach (Match match in matches)
                {
                    doc.TextArea.IndicatorFillRange(match.Index, match.Length);
                }
                textBoxMatches.Text = matches.Count + " matches found";
            }
            else
            {
                textBoxMatches.Text = "";
            }
            doc.TextArea.CurrentPosition = 0;
            doc.TextArea.ScrollCaret();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Topmost = true;
            Activate();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Active = true;
            GetFiles();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Active = false;
        }
    }

    public class SearchFile : IComparable
    {
        public string FileName { get; set; }
        public int LineCount { get; set; }
        public DateTime Date { get; set; }
        public string Folder { get; set; }

        public string FilePath;
        public IEnumerable<string> Text;

        public SearchFile(string fileName)
        {
            FilePath = fileName;
            Folder = System.IO.Path.GetDirectoryName(FilePath);
            FileName = System.IO.Path.GetFileNameWithoutExtension(FilePath);
            Text = File.ReadLines(FilePath);
            LineCount = Text.Count();
            Date = File.GetLastWriteTime(FilePath);
        }

        public int CompareTo(Object obj)
        {
            return FileName.CompareTo(((SearchFile)obj).FileName);
        }
    }
}