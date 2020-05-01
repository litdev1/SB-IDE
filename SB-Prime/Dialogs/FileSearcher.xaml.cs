﻿using ScintillaNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Input;

namespace SB_Prime.Dialogs
{
    /// <summary>
    /// Interaction logic for FileSearcher.xaml
    /// </summary>
    public partial class FileSearcher : Window, IDisposable
    {
        private PleaseWait pleaseWait = new PleaseWait(Properties.Strings.String117 + "\n" + Properties.Strings.String118);
        private List<SearchFile> searchFiles = new List<SearchFile>();
        private List<SearchFile> filterFiles = new List<SearchFile>();
        public static string RootPath = "";
        public static bool Active = false;
        public static int ProgressState = 0; //0-ready to load 1-loading 2-completed
        public static string ProgressDir = "";
        public static int ProgressCount = 0;
        public static int ProgressFailed = 0;
        private static Stack<string> dirs = new Stack<string>();
        private static List<string> files = new List<string>();
        private Progress dlg;
        private System.Threading.Timer timer;
        //private System.Windows.Input.Cursor cursor;


        public FileSearcher()
        {
            InitializeComponent();

            Topmost = MainWindow.topmost;
            FontSize = 12 + MainWindow.zoom;

            textBoxSearcherRoot.Text = RootPath;
            checkBoxSearcherWord.IsChecked = true;
            checkBoxSearcherCase.IsChecked = false;
            checkBoxSearcherPath.IsChecked = true;

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
            if (ProgressState != 0) return;
            ProgressState = 1;
            Thread worker = new Thread(new ThreadStart(GetFilesWorker));
            worker.Start();
            dlg = new Progress();
            dlg.Owner = GetWindow(this);
            dlg.Show();
        }

        private void GetFilesWorker()
        {
            try
            {
                dirs.Clear();
                files.Clear();
                ProgressCount = 0;
                ProgressFailed = 0;

                dirs.Push(RootPath);

                while (ProgressState == 1 && dirs.Count > 0)
                {
                    try
                    {
                        string dir = dirs.Pop();
                        ProgressDir = dir;
                        string[] _dirs = Directory.GetDirectories(dir);
                        foreach (string _dir in _dirs)
                        {
                            dirs.Push(_dir);
                        }
                        string[] _files = Directory.GetFiles(dir);
                        foreach (string _file in _files)
                        {
                            if (_file.EndsWith(".sb") || _file.EndsWith(".smallbasic"))
                            {
                                files.Add(_file);
                                ProgressCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ProgressFailed++;
                            //MainWindow.Errors.Add(new Error("File Searcher : " + ex.Message));
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MainWindow.Errors.Add(new Error("File Searcher : " + ex.Message));
                });
            }
            ProgressState = 2;
            Dispatcher.Invoke(() =>
            {
                pleaseWait.Owner = GetWindow(this);
                pleaseWait.Show();
            });
        }

        private void Filter()
        {
            if (!Directory.Exists(RootPath)) return;

            string keyword = textBoxSearcherText.Text;
            filterFiles.Clear();
            RegexOptions caseSensitive = (bool)checkBoxSearcherCase.IsChecked ? RegexOptions.None : RegexOptions.IgnoreCase;
            string wholeWord = (bool)checkBoxSearcherWord.IsChecked ? "\\b" + keyword + "\\b" : keyword;
            foreach (SearchFile file in searchFiles)
            {
                bool isFound = Regex.IsMatch(string.Concat(file.Text), wholeWord, caseSensitive);
                if ((bool)checkBoxSearcherPath.IsChecked)
                {
                    if ((bool)checkBoxSearcherCase.IsChecked) isFound |= file.FilePath.Contains(keyword);
                    else isFound |= file.FilePath.ToUpperInvariant().Contains(keyword.ToUpperInvariant());
                }
                if (keyword == "" || isFound) filterFiles.Add(file);
            }
            dataGridSearcher.ItemsSource = filterFiles;
            dataGridSearcher.Items.Refresh();
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
                RegexOptions caseSensitive = (bool)checkBoxSearcherCase.IsChecked ? RegexOptions.None : RegexOptions.IgnoreCase;
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
            Topmost = MainWindow.topmost;
            Activate();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Active = true;
            ProgressState = 0;
            GetFiles();
            timer = new System.Threading.Timer(new TimerCallback(_timer), null, 100, 100);
        }

        private void _timer(object state)
        {
            Dispatcher.Invoke(() =>
            {
                buttonSearcherBrowse.IsEnabled = ProgressState == 0;
                buttonSearcherFilter.IsEnabled = ProgressState == 0;
                if (ProgressState == 0 && files.Count != searchFiles.Count)
                {
                    ProgressState = 2; // don't understand how it ever gets here?  Fixed by stoppping timer on close
                }
                if (ProgressState == 2)
                {
                    ProgressState = 0;
                    searchFiles.Clear();
                    foreach (string file in files)
                    {
                        searchFiles.Add(new SearchFile(file));
                    }
                    searchFiles.Sort();
                    dataGridSearcher.ItemsSource = searchFiles;
                    dataGridSearcher.Items.Refresh();
                    textBoxCount.Text = searchFiles.Count + " files found";
                    pleaseWait.Hide();
                }
            });
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Active = false;
            timer.Dispose();
            ProgressState = 0;
        }

        private void textBoxSearcherRoot_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                RootPath = textBoxSearcherRoot.Text;
                GetFiles();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    timer.Dispose();
                    dlg.Dispose();
                }
                catch { }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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
