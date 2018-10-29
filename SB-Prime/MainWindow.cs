using ExtensionManagerLibrary;
using SB_Prime.Dialogs;
using ScintillaNET;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms.Integration;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Shell;
using System.Configuration;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Windows.Controls.Ribbon;
using System.Windows.Media.Effects;

namespace SB_Prime
{
    public partial class MainWindow
    {
        public static MainWindow THIS;
        public static ObservableCollection<Error> Errors = new ObservableCollection<Error>();
        public static SBObject showObject = null;
        public static SBObject showObjectLast = null;
        public static Member showMember = null;
        public static Member showMemberLast = null;
        public static string InstallDir = "";
        public static string ImportProgram = "";
        public static bool ignoreBP = false;
        public static bool dualScreen = false;
        public static bool wrap = false;
        public static bool indent = true;
        public static bool whitespace = false;
        public static bool highlightAll = false;
        public static int zoom = 0;
        public static int theme = 0;
        public static bool quoteInserts = false;
        public static bool hexColors = false;
        public static bool loadExtensions = true;
        public static SearchFlags searchFlags = SearchFlags.None;
        public static Size size = new Size(double.PositiveInfinity, double.PositiveInfinity);
        public static bool CompileError = false;
        public static Queue<TabItem> MarkedForDelete = new Queue<TabItem>();
        public static Queue<string> MarkedForOpen = new Queue<string>();
        public static Queue<string> MarkedForWatch = new Queue<string>();
        public static Queue<Action> MarkedForHotKey = new Queue<Action>();
        public static bool GetStackVariables = false;
        public static Dictionary<string, List<int>> breakpoints = new Dictionary<string, List<int>>();
        public static Dictionary<string, List<int>> bookmarks = new Dictionary<string, List<int>>();
        public static int MaxMRU = 50;
        public static char DelimBP = '#';

        public ObservableCollection<DebugData> debugData = new ObservableCollection<DebugData>();
        public SBInterop sbInterop;
        SBplugin sbPlugin;
        SBDocument activeDocument;
        TabItem activeTab;
        Timer threadTimer;
        bool debugUpdated = false;
        bool highlightsUpdated = false;
        object lockRun = new object();

        private void InitWindow()
        {
            THIS = this;

            statusVersion.Content = "SB-Prime Version " + Assembly.GetExecutingAssembly().GetName().Version + " (Debug Extension " + SBInterop.CurrentVersion + ")";
            Errors.Add(new Error("Welcome to Small Basic Prime"));

            sbInterop = new SBInterop();
            sbPlugin = new SBplugin(this);

            // CREATE CONTROLS
            for (int i = tabControlSB1.Items.Count - 1; i >= 0; i--) tabControlSB1.Items.RemoveAt(i);
            for (int i = tabControlSB2.Items.Count - 1; i >= 0; i--) tabControlSB2.Items.RemoveAt(i);
            documentGrid.ColumnDefinitions[1].MaxWidth = dualScreen ? 6 : 0;
            documentGrid.ColumnDefinitions[2].MaxWidth = dualScreen ? double.PositiveInfinity : 0;

            toggleSplit.IsChecked = dualScreen;
            toggleWrap.IsChecked = wrap;
            toggleIndent.IsChecked = indent;
            toggleWhiteSpace.IsChecked = whitespace;
            toggleHighlight.IsChecked = highlightAll;
            toggleWholeWord.IsChecked = searchFlags.HasFlag(SearchFlags.WholeWord);
            toggleCaseSensitive.IsChecked = searchFlags.HasFlag(SearchFlags.MatchCase);
            toggleTheme.IsChecked = theme > 0;
            viewLanguage.Text = SBInterop.Language;

            // DEFAULT FILE
            AddDocument(1);
            AddDocument(2);
            Activate(tabControlSB1);
            tabControlSB1.Focus();
            App app = (App)Application.Current;
            for (int i = 0; i < app.Arguments.Length; i++)
            {
                if (i == 0)
                {
                    activeDocument.LoadDataFromFile(app.Arguments[i]);
                    activeTab.Header = new TabHeader(app.Arguments[i]);
                    SetTabHeaderStyle(activeTab);
                }
                else
                {
                    MarkedForOpen.Enqueue(app.Arguments[i]);
                }
            }

            dataGridResults.Tag = 0;
            dataGridResults.ItemsSource = Errors;
            ContextMenu menu = new ContextMenu();
            dataGridResults.ContextMenu = menu;
            MenuItem clear = new MenuItem();
            menu.Items.Add(clear);
            clear.Header = "Clear";
            clear.Icon = new Image()
            {
                Width = 14,
                Height = 14,
                Source = ImageSourceFromBitmap(Properties.Resources.Erase)
            };
            clear.Click += new RoutedEventHandler(GridResultsClick);

            menu = new ContextMenu();
            dataGridDebug.ContextMenu = menu;
            clear = new MenuItem();
            menu.Items.Add(clear);
            clear.Header = "Clear";
            clear.Icon = new Image()
            {
                Width = 14,
                Height = 14,
                Source = ImageSourceFromBitmap(Properties.Resources.Erase)
            };
            clear.Click += new RoutedEventHandler(GridDebugClick);
            clear = new MenuItem();
            menu.Items.Add(clear);
            clear.Header = "Clear Watch Conditions";
            clear.Icon = new Image()
            {
                Width = 14,
                Height = 14,
                Source = ImageSourceFromBitmap(Properties.Resources.Delete_frame)
            };
            clear.Click += new RoutedEventHandler(GridDebugClick);

            CollectionViewSource itemCollectionViewSource;
            itemCollectionViewSource = (CollectionViewSource)(FindResource("DebugDataSource"));
            itemCollectionViewSource.Source = debugData;

            SetWindowColors();

            threadTimer = new Timer(new TimerCallback(ThreadTimerCallback));
            threadTimer.Change(100, 100);
        }

        public SBDocument GetActiveDocument()
        {
            return activeDocument;
        }

        public TabItem GetActiveTab()
        {
            return activeTab;
        }

        private void SetWindowColors()
        {
            Resources["GridBrushBackground"] = new SolidColorBrush(IntToColor(BACKGROUND_COLOR));
            Resources["GridBrushForeground"] = new SolidColorBrush(IntToColor(FOREGROUND_COLOR));
            Resources["SplitterBrush"] = new SolidColorBrush(IntToColor(SPLITTER_COLOR));
            dataGridResults.Items.Refresh();
            dataGridDebug.Items.Refresh();
        }

        private void InitIntellisense()
        {
            canvasInfo.Children.Clear();
            IntellisenseToggle();

            TextBlock tb = new TextBlock()
            {
                Text = "Intellisense",
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 24 + zoom
        };
            canvasInfo.Children.Add(tb);
            tb.Measure(size);
            double canvasWidth = viewGrid.ColumnDefinitions[2].MaxWidth - 10; // Math.Max(canvasInfo.ActualWidth, Math.Max(20 + tb.DesiredSize.Width, 200));
            Canvas.SetLeft(tb, (canvasWidth - tb.DesiredSize.Width) / 2);
            Canvas.SetTop(tb, 25);

            tb = new TextBlock()
            {
                Text = "Intellisense will appear here when you hover over objects and methods as well as when you type and view potential options.\n\n" +
                "Additionally, a popup description for methods can be viewed in this window by hovering the mouse over methods, properties or events when viewing an object.",
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                FontSize = 14 + zoom,
                Width = (canvasWidth - 50)
            };
            canvasInfo.Children.Add(tb);
            Canvas.SetLeft(tb, 25);
            Canvas.SetTop(tb, 75);

            canvasInfo.MinHeight = 100 + tb.DesiredSize.Height;
        }

        private void IntellisenseToggle()
        {
            Image img = new Image()
            {
                Width = 20,
                Height = 20,
                Source = ImageSourceFromBitmap(Properties.Resources.Help_book),
                Stretch = Stretch.Fill
            };
            Button button = new Button() { Content = img,
                Width = 24,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 4, 0),
                Background = new SolidColorBrush(Colors.Transparent), BorderBrush = new SolidColorBrush(Colors.Transparent)
            };
            button.ToolTip = "Toggle intellisense view";
            button.Click += new RoutedEventHandler(ClickIntellisenseToggle);
            wrapperGrid.Children.Add(button);

            Image img2 = new Image()
            {
                Width = 20,
                Height = 20,
                Source = ImageSourceFromBitmap(Properties.Resources.Monitors),
                Stretch = Stretch.Fill
            };
            Button button2 = new Button() { Content = img2,
                Width = 24,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 32, 0),
                Background = new SolidColorBrush(Colors.Transparent), BorderBrush = new SolidColorBrush(Colors.Transparent)
            };
            button2.ToolTip = "Toggle split screen program layout";
            button2.Click += new RoutedEventHandler(viewDual_Click);
            wrapperGrid.Children.Add(button2);

            Image img3 = new Image()
            {
                Width = 20,
                Height = 20,
                Source = ImageSourceFromBitmap(Properties.Resources.Details),
                Stretch = Stretch.Fill
            };
            Button button3 = new Button()
            {
                Content = img3,
                Width = 24,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 60, 0),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderBrush = new SolidColorBrush(Colors.Transparent)
            };
            button3.ToolTip = "Imported program details";
            button3.Click += new RoutedEventHandler(details_Click);
            wrapperGrid.Children.Add(button3);

            Image img4 = new Image()
            {
                Width = 20,
                Height = 20,
                Source = ImageSourceFromBitmap(Properties.Resources.Difference),
                Stretch = Stretch.Fill
            };
            Button button4 = new Button()
            {
                Content = img4,
                Width = 24,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 88, 0),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderBrush = new SolidColorBrush(Colors.Transparent)
            };
            button4.ToolTip = "Toggle difference files in split screen";
            button4.Click += new RoutedEventHandler(difference_Click);
            wrapperGrid.Children.Add(button4);
            wrapperGrid.Children[4].Visibility = dualScreen ? Visibility.Visible : Visibility.Hidden;
        }

        private void details_Click(object sender, RoutedEventArgs e)
        {
            Details details = new Details(((TabHeader)activeTab.Header).FileName, sbInterop);
            details.ShowDialog();
        }

        private void difference_Click(object sender, RoutedEventArgs e)
        {
            SBDiff.UpdateDiff();
            wrapperGrid.Children[4].Effect = SBDiff.bShowDiff ? new DropShadowEffect
            {
                Color = Colors.DarkCyan,
                Direction = 0,
                ShadowDepth = 0,
                BlurRadius = 8,
                Opacity = 1,
            } : null;
        }

        private void ClickIntellisenseToggle(object sender, RoutedEventArgs e)
        {
            if (viewGrid.ColumnDefinitions[2].Width.Value > 0)
            {
                viewGrid.ColumnDefinitions[2].Width = new GridLength(0);
            }
            else
            {
                viewGrid.ColumnDefinitions[2].Width = new GridLength(viewGrid.ColumnDefinitions[2].MaxWidth);
            }
        }

        private void GridDebugClick(object sender, RoutedEventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            if ((string)item.Header == "Clear Watch Conditions")
            {
                foreach (DebugData data in debugData)
                {
                    data.ClearWatch();
                }
            }
            else
            {
                debugData.Clear();
            }
        }

        private void GridResultsClick(object sender, RoutedEventArgs e)
        {
            Errors.Clear();
        }

        private void LoadSettings()
        {
            Top = Properties.Settings.Default.Top;
            Left = Properties.Settings.Default.Left;
            Height = Properties.Settings.Default.Height;
            Width = Properties.Settings.Default.Width;
            // Very quick and dirty - but it does the job
            if (Properties.Settings.Default.Maximized)
            {
                WindowState = WindowState.Maximized;
            }

            int i;
            dualScreen = Properties.Settings.Default.SplitScreen;
            wrap = Properties.Settings.Default.WordWrap;
            indent = Properties.Settings.Default.IndentGuides;
            whitespace = Properties.Settings.Default.WhiteSpace;
            highlightAll = Properties.Settings.Default.HighlightAll;
            zoom = Properties.Settings.Default.Zoom;
            theme = Properties.Settings.Default.Theme;
            quoteInserts = Properties.Settings.Default.QuoteInserts;
            hexColors = Properties.Settings.Default.HEXColors;
            searchFlags = (SearchFlags)Properties.Settings.Default.SearchFlags;
            SBInterop.Language = Properties.Settings.Default.Language;
            SBInterop.Version = Properties.Settings.Default.Version;
            debugData.Clear();
            for (i = 0; i < Properties.Settings.Default.WatchList.Count; i++)
            {
                DebugData data = new DebugData();
                data.Group = Properties.Settings.Default.WatchList[i];
                debugData.Add(data);
            }
            FileSearcher.RootPath = Properties.Settings.Default.RootPath;
            InstallDir = Properties.Settings.Default.InstallDir;
            mainGrid.RowDefinitions[2].Height = new GridLength(Properties.Settings.Default.OutputHeight > 0 ? Properties.Settings.Default.OutputHeight : 150);
            viewGrid.ColumnDefinitions[2].Width = new GridLength(Properties.Settings.Default.IntellisenseWidth > 0 ? viewGrid.ColumnDefinitions[2].MaxWidth : 0);
            var ideColors = IDEColors;
            for (i = 0; i < Properties.Settings.Default.Colors.Count; i++)
            {
                string[] data = Properties.Settings.Default.Colors[i].Split('?');
                if (data.Length != 2) continue;
                int value = 0;
                int.TryParse(data[1], out value);
                ideColors[data[0]] = value;
            }
            IDEColors = ideColors;

            for (i = Properties.Settings.Default.MRU.Count - 1; i >= 0; i--)
            {
                if (!File.Exists(Properties.Settings.Default.MRU[i])) Properties.Settings.Default.MRU.RemoveAt(i);
            }
            for (i = 0; i < Properties.Settings.Default.MRU.Count; i++)
            {
                MRUlst.Add(new MRUdata() { Ellipsis = Ellipsis(Properties.Settings.Default.MRU[i]) });
            }

            breakpoints.Clear();
            foreach (string breakpoint in Properties.Settings.Default.Breakpoints)
            {
                List<int> lines = new List<int>();
                string[] data = breakpoint.Split(new char[] { DelimBP }, StringSplitOptions.RemoveEmptyEntries);
                int line = -1;
                for (i = 1; i < data.Length; i++) if (int.TryParse(data[i], out line)) lines.Add(line);
                if (File.Exists(data[0]) && lines.Count > 0) breakpoints[data[0]] = lines;
            }
            bookmarks.Clear();
            foreach (string bookmark in Properties.Settings.Default.Bookmarks)
            {
                List<int> lines = new List<int>();
                string[] data = bookmark.Split(new char[] { DelimBP }, StringSplitOptions.RemoveEmptyEntries);
                int line = -1;
                for (i = 1; i < data.Length; i++) if (int.TryParse(data[i], out line)) lines.Add(line);
                if (File.Exists(data[0]) && lines.Count > 0) bookmarks[data[0]] = lines;
            }
            loadExtensions = Properties.Settings.Default.LoadExtensions;
        }

        private void ResetSettings()
        {
            Properties.Settings.Default.Reset();
            var ideColors = IDEColors;
            ideColors.Clear();
            for (int i = 0; i < DefaultColors.Count; i++)
            {
                ideColors[DefaultColors.ElementAt(i).Key] = DefaultColors.ElementAt(i).Value;
            }
            IDEColors = ideColors;
            debugData.Clear();
            LoadSettings();
        }

        private void HighLightAll()
        {
            highlightAll = !highlightAll;
            activeDocument.searchManager.HighLight(highlightAll ? activeDocument.searchManager.LastHighLight : "");
        }

        private Grid Ellipsis(string txt)
        {
            TextBlock tb = new TextBlock() { Text = txt, FontSize = 14 + zoom, FontWeight = FontWeights.DemiBold };
            tb.ToolTip = txt;
            tb.Text = Path.GetFileNameWithoutExtension(txt);

            ImageSource imgSource = ImageSourceFromBitmap(Properties.Resources.AppIcon);
            Image img = new Image()
            {
                Width = 20,
                Height = 20,
                Source = imgSource
            };

            Grid grid = new Grid();
            grid.Children.Add(img);
            grid.Children.Add(tb);
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(24) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength() });
            Grid.SetColumn(img, 0);
            Grid.SetColumn(tb, 1);
            grid.Tag = txt;

            return grid;
        }

        private void SaveSettings()
        {
            if (WindowState == WindowState.Maximized)
            {
                // Use the RestoreBounds as the current values will be 0, 0 and the size of the screen
                Properties.Settings.Default.Top = RestoreBounds.Top;
                Properties.Settings.Default.Left = RestoreBounds.Left;
                Properties.Settings.Default.Height = RestoreBounds.Height;
                Properties.Settings.Default.Width = RestoreBounds.Width;
                Properties.Settings.Default.Maximized = true;
            }
            else
            {
                Properties.Settings.Default.Top = Top;
                Properties.Settings.Default.Left = Left;
                Properties.Settings.Default.Height = Height;
                Properties.Settings.Default.Width = Width;
                Properties.Settings.Default.Maximized = false;
            }

            for (int i = Properties.Settings.Default.MRU.Count - 1; i >= 0; i--)
            {
                if (!File.Exists(Properties.Settings.Default.MRU[i])) Properties.Settings.Default.MRU.RemoveAt(i);
            }
            foreach (TabItem tab in tabControlSB2.Items)
            {
                SBDocument doc = (SBDocument)tab.Tag;
                if (Properties.Settings.Default.MRU.Contains(doc.Filepath)) Properties.Settings.Default.MRU.Remove(doc.Filepath);
                if (File.Exists(doc.Filepath)) Properties.Settings.Default.MRU.Insert(0, doc.Filepath);
            }
            foreach (TabItem tab in tabControlSB1.Items)
            {
                SBDocument doc = (SBDocument)tab.Tag;
                if (Properties.Settings.Default.MRU.Contains(doc.Filepath)) Properties.Settings.Default.MRU.Remove(doc.Filepath);
                if (File.Exists(doc.Filepath)) Properties.Settings.Default.MRU.Insert(0, doc.Filepath);
            }
            for (int i = Properties.Settings.Default.MRU.Count - 1; i >= MaxMRU; i--) Properties.Settings.Default.MRU.RemoveAt(i);

            Properties.Settings.Default.SplitScreen = dualScreen;
            Properties.Settings.Default.WordWrap = wrap;
            Properties.Settings.Default.IndentGuides = indent;
            Properties.Settings.Default.WhiteSpace = whitespace;
            Properties.Settings.Default.HighlightAll = highlightAll;
            Properties.Settings.Default.Zoom = zoom;
            Properties.Settings.Default.Theme = theme;
            Properties.Settings.Default.QuoteInserts = quoteInserts;
            Properties.Settings.Default.HEXColors = hexColors;
            Properties.Settings.Default.SearchFlags = (int)searchFlags;
            Properties.Settings.Default.Language = SBInterop.Language;
            Properties.Settings.Default.Version = SBInterop.Version;
            Properties.Settings.Default.WatchList.Clear();
            for (int i = 0; i < debugData.Count; i++)
            {
                Properties.Settings.Default.WatchList.Add(debugData[i].Group);
            }
            Properties.Settings.Default.RootPath = FileSearcher.RootPath;
            Properties.Settings.Default.InstallDir = InstallDir;
            Properties.Settings.Default.OutputHeight = mainGrid.RowDefinitions[2].ActualHeight;
            Properties.Settings.Default.IntellisenseWidth = viewGrid.ColumnDefinitions[2].ActualWidth;
            Properties.Settings.Default.Colors.Clear();
            foreach (KeyValuePair<string,int> kvp in IDEColors)
            {
                Properties.Settings.Default.Colors.Add(kvp.Key + "?" + kvp.Value);
            }

            foreach (TabItem tab in tabControlSB1.Items)
            {
                activeTab = tab;
                activeDocument = GetDocument();
                activeDocument.SetMarks();
            }
            foreach (TabItem tab in tabControlSB2.Items)
            {
                activeTab = tab;
                activeDocument = GetDocument();
                activeDocument.SetMarks();
            }

            Properties.Settings.Default.Breakpoints.Clear();
            foreach (KeyValuePair<string, List<int>> kvp in breakpoints)
            {
                string data = kvp.Key;
                foreach (int line in kvp.Value) data += DelimBP.ToString() + line;
                Properties.Settings.Default.Breakpoints.Add(data);
            }
            Properties.Settings.Default.Bookmarks.Clear();
            foreach (KeyValuePair<string, List<int>> kvp in bookmarks)
            {
                string data = kvp.Key;
                foreach (int line in kvp.Value) data += DelimBP.ToString() + line;
                Properties.Settings.Default.Bookmarks.Add(data);
            }
            Properties.Settings.Default.LoadExtensions = loadExtensions;

            Properties.Settings.Default.Save();
        }

        private void ExportSettings()
        {
            System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            saveFileDialog.FileName = "SB-Prime";
            saveFileDialog.Filter = "Settings files (*.config)|*.config|All files (*.*)|*.*";
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.RestoreDirectory = true;
            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ExportSettingsToFile(saveFileDialog.FileName);
            }
        }

        public void ExportSettingsToFile(string file)
        {
            SaveSettings();
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
            config.SaveAs(file);
        }

        private void ImportSettings()
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Settings files (*.config)|*.config|All files (*.*)|*.*";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ImportSettingsFromFile(openFileDialog.FileName);
            }
        }

        public void ImportSettingsFromFile(string file)
        {
            var appSettings = Properties.Settings.Default;
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);

                string appSettingsXmlName = Properties.Settings.Default.Context["GroupName"].ToString();
                // returns "MyApplication.Properties.Settings";

                // Open settings file as XML
                var import = XDocument.Load(file);
                // Get the whole XML inside the settings node
                var settings = import.XPathSelectElements("//" + appSettingsXmlName);

                config.GetSectionGroup("userSettings")
                    .Sections[appSettingsXmlName]
                    .SectionInformation
                    .SetRawXml(settings.Single().ToString());
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("userSettings");

                appSettings.Reload();
                LoadSettings();
            }
            catch (Exception ex) // Should make this more specific
            {
                // Could not import settings.
                Errors.Add(new Error("Import Settings : " + ex.Message));
                appSettings.Reload(); // from last set saved, not defaults
            }
        }

        private bool CloseTab(TabControl tabControl) // true is cancel
        {
            int count = tabControl.Items.Count;
            for (int i = count - 1; i >= 0; i--)
            {
                tabControl.SelectedIndex = i;
                activeTab = (TabItem)tabControl.Items[i];
                activeDocument = GetDocument();
                if (DeleteDocument() == System.Windows.Forms.DialogResult.Cancel)
                {
                    return true;
                }
            }
            return false;
        }

        private void SaveDocumentAs()
        {
            System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            saveFileDialog.FileName = ((TabHeader)activeTab.Header).FileName;
            saveFileDialog.Filter = "Small Basic files (*.sb)|*.sb|Formatted HTML (*.html)|*.html|All files (*.*)|*.*";
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.RestoreDirectory = true;
            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (saveFileDialog.FileName.ToLower().EndsWith(".sb"))
                {
                    activeDocument.SaveDataToFile(saveFileDialog.FileName);
                    activeTab.Header = new TabHeader(saveFileDialog.FileName);
                    SetTabHeaderStyle(activeTab);
                    if (Properties.Settings.Default.MRU.Contains(activeDocument.Filepath)) Properties.Settings.Default.MRU.Remove(activeDocument.Filepath);
                    if (File.Exists(activeDocument.Filepath)) Properties.Settings.Default.MRU.Insert(0, activeDocument.Filepath);
                }
                else if (saveFileDialog.FileName.ToLower().EndsWith(".html"))
                {
                    string html = activeDocument.TextArea.GetTextRangeAsHtml(0, activeDocument.TextArea.TextLength);
                    File.WriteAllText(saveFileDialog.FileName, html);
                }
            }
        }

        private void AddDocument(int iTab = -1)
        {
            List<int> nums = new List<int>();
            foreach (TabItem tabItem in tabControlSB1.Items)
            {
                if (((TabHeader)tabItem.Header).FileName.StartsWith("Untitled"))
                {
                    int i;
                    int.TryParse(((TabHeader)tabItem.Header).FileName.Substring(8), out i);
                    nums.Add(i);
                }
            }
            foreach (TabItem tabItem in tabControlSB2.Items)
            {
                if (((TabHeader)tabItem.Header).FileName.StartsWith("Untitled"))
                {
                    int i;
                    int.TryParse(((TabHeader)tabItem.Header).FileName.Substring(8), out i);
                    nums.Add(i);
                }
            }
            int num = 1;
            while (nums.Contains(num)) num++;
            WindowsFormsHost host = new WindowsFormsHost();
            System.Windows.Forms.Panel panel = new System.Windows.Forms.Panel();
            activeDocument = new SBDocument();
            panel.Contains(activeDocument.TextArea);
            host.Child = activeDocument.TextArea;
            activeDocument.TextArea.PreviewKeyDown += Window_PreviewKeyDown;
            GetTabContol(iTab).Items.Add(new TabItem());
            activeTab = (TabItem)GetTabContol(iTab).Items[GetTabContol(iTab).Items.Count - 1];
            activeTab.Content = host;
            activeTab.Header = new TabHeader("Untitled" + num);
            SetTabHeaderStyle(activeTab);
            activeTab.Tag = activeDocument;
            activeDocument.Tab = activeTab;
            activeDocument.WrapMode = wrap ? WrapMode.Whitespace : WrapMode.None;
            activeDocument.IndentationGuides = indent ? IndentView.LookBoth : IndentView.None;
            activeDocument.ViewWhitespace = whitespace ? WhitespaceMode.VisibleAlways : WhitespaceMode.Invisible;
            activeDocument.TextArea.Zoom = zoom;
            activeDocument.Theme = theme;

            GetTabContol(iTab).SelectedIndex = GetTabContol(iTab).Items.Count - 1;
            activeTab.Focus();
        }

        private System.Windows.Forms.DialogResult DeleteDocument()
        {
            if (null != activeDocument.debug) activeDocument.debug.Dispose();

            if (activeDocument.IsDirty)
            {
                System.Windows.Forms.DialogResult dlg = System.Windows.Forms.MessageBox.Show("The text in " + ((TabHeader)activeTab.Header).FileName + " has changed.\n\nDo you want to save the changes?", "SB-Prime", System.Windows.Forms.MessageBoxButtons.YesNoCancel, System.Windows.Forms.MessageBoxIcon.Question);
                if (dlg == System.Windows.Forms.DialogResult.Cancel) return dlg;
                else if (dlg == System.Windows.Forms.DialogResult.Yes)
                {
                    if (activeDocument.Filepath != "")
                    {
                        activeDocument.SaveDataToFile();
                    }
                    else
                    {
                        System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog();
                        saveFileDialog.FileName = ((TabHeader)activeTab.Header).FileName;
                        saveFileDialog.Filter = "Small Basic files (*.sb)|*.sb|All files (*.*)|*.*";
                        saveFileDialog.FilterIndex = 1;
                        saveFileDialog.RestoreDirectory = true;
                        dlg = saveFileDialog.ShowDialog();
                        if (dlg == System.Windows.Forms.DialogResult.Cancel) return dlg;
                        else if (dlg == System.Windows.Forms.DialogResult.OK)
                        {
                            activeDocument.SaveDataToFile(saveFileDialog.FileName);
                            activeTab.Header = new TabHeader(System.IO.Path.GetFileName(saveFileDialog.FileName));
                            SetTabHeaderStyle(activeTab);
                        }
                    }
                }
            }
            int curTabControl = GetTabContol() == tabControlSB1 ? 1 : 2;
            if (GetTabContol().Items.Count > 1)
            {
                int nextSelected = Math.Max(0, GetTabContol().SelectedIndex - 1);
                GetTabContol(curTabControl).Items.Remove(activeTab);
                activeTab = (TabItem)GetTabContol(curTabControl).Items[nextSelected];
                activeDocument = GetDocument();
            }
            else
            {
                GetTabContol(curTabControl).Items.Remove(activeTab);
                AddDocument(curTabControl);
            }
            return System.Windows.Forms.DialogResult.OK;
        }

        private TabControl GetTabContol(int iTab)
        {
            if (iTab < 0) return GetTabContol();
            else return iTab == 1 ? tabControlSB1 : tabControlSB2;
        }

        private TabControl GetTabContol()
        {
            return (TabControl)activeTab.Parent;
        }

        private SBDocument GetDocument()
        {
            SetFocus();
            return (SBDocument)activeTab.Tag;
        }

        private void SetFocus()
        {
            if (null == activeTab || null == activeDocument) return;
            ((WindowsFormsHost)activeTab.Content).Child.Focus();
        }

        private void Activate(TabControl tabControl)
        {
            if (tabControl.SelectedIndex >= 0)
            {
                activeTab = (TabItem)tabControl.Items[tabControl.SelectedIndex];
                activeDocument = GetDocument();
            }
        }

        private void ThreadTimerCallback(object state)
        {
            if (null == activeTab || null == activeDocument) return;
            try
            {
                if (CheckAccess())
                {
                    UpdateTabHeader();
                    UpdateDebug();
                    UpdateOutput();
                    UpdateRun();
                    UpdateIntellisense(showObject, showMember);
                    UpdateFileSeracher();
                    UpdateStatusBar();
                    UpdateZoom();
                    UpdateHotKey();
                    UpdateLine();
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateTabHeader();
                        UpdateDebug();
                        UpdateOutput();
                        UpdateRun();
                        UpdateIntellisense(showObject, showMember);
                        UpdateFileSeracher();
                        UpdateStatusBar();
                        UpdateZoom();
                        UpdateHotKey();
                        UpdateLine();
                    });
                }
            }
            catch (Exception ex)
            {

            }
        }

        private void UpdateLine()
        {
            activeDocument.lineStack.PushBackwards(activeDocument.TextArea.CurrentLine);
            EditPrevious.IsEnabled = activeDocument.lineStack.backwards.Count > 1;
            EditNext.IsEnabled = activeDocument.lineStack.forwards.Count > 0;
        }

        private void UpdateHotKey()
        {
            if (MarkedForHotKey.Count > 0)
            {
                MarkedForHotKey.Dequeue().Method.Invoke(this, null);
            }
        }

        private void UpdateTabHeader()
        {
            TabHeader tabHeader = ((TabHeader)activeTab.Header);
            if (null != tabHeader)
            {
                tabHeader.SetDirty(activeDocument.IsDirty);
                if (MarkedForDelete.Count > 0)
                {
                    TabControl selectedTab = GetTabContol();
                    int selectedIndex = selectedTab.SelectedIndex;

                    activeTab = MarkedForDelete.Dequeue();
                    activeDocument = GetDocument();
                    int deletedIndex = GetTabContol().Items.IndexOf(activeTab);
                    GetTabContol().SelectedIndex = deletedIndex;
                    DeleteDocument();

                    selectedTab.SelectedIndex = GetTabContol() != selectedTab || deletedIndex > selectedIndex ? selectedIndex : selectedIndex - 1;
                    Activate(selectedTab);
                }
            }
        }

        private void UpdateDebug()
        {
            if (MarkedForWatch.Count > 0)
            {
                debugData.Add(new DebugData() { Variable = MarkedForWatch.Dequeue() });
            }
            if (null == activeDocument.debug || !activeDocument.debug.IsPaused())
            {
                if (!highlightsUpdated)
                {
                    activeDocument.ClearHighlights();
                    highlightsUpdated = true;
                }
                debugUpdated = false;
                return;
            }
            else
            {
                highlightsUpdated = false;
            }

            if (!debugUpdated)
            {
                activeDocument.debug.ClearConditions();
                foreach (DebugData data in debugData)
                {
                    activeDocument.debug.GetValue(data.Variable);
                    activeDocument.debug.SetCondition(data);
                }
                if (GetStackVariables)
                {
                    activeDocument.debug.GetStack();
                    activeDocument.debug.GetVariables();
                }
                debugUpdated = true;
            }
        }

        private void UpdateOutput()
        {
            if ((int)dataGridResults.Tag != dataGridResults.Items.Count)
            {
                dataGridResults.Tag = dataGridResults.Items.Count;
                if (dataGridResults.Items.Count > 0) dataGridResults.ScrollIntoView(dataGridResults.Items[dataGridResults.Items.Count - 1]);
            }
            if (CompileError)
            {
                tabControlResults.SelectedItem = tabOutput;
                CompileError = false;
            }
        }

        private void UpdateRun()
        {
            try
            {
                if (null != activeDocument.Proc && activeDocument.Proc.HasExited)
                {
                    activeDocument.ClearHighlights();
                    Errors.Add(new Error("Run : " + "Successfully terminated run with process " + activeDocument.Proc.Id));
                    activeDocument.Proc = null;
                    if (null == activeDocument.debug) return;
                    activeDocument.debug.Dispose();
                    activeDocument.debug = null;
                }
            }
            catch
            {

            }
        }

        private void UpdateIntellisense(SBObject obj, Member member)
        {
            try
            {
                double left = 10;
                double top = 10;

                if (null != obj && obj != showObjectLast)
                {
                    canvasInfo.Children.Clear();

                    Image img = new Image()
                    {
                        Width = 50,
                        Height = 50,
                        Source = ImageSourceFromBitmap(Properties.Resources.IntellisenseObject)
                    };
                    canvasInfo.Children.Add(img);
                    Canvas.SetLeft(img, left);
                    Canvas.SetTop(img, top);

                    TextBlock tb = new TextBlock()
                    {
                        Text = obj.name,
                        Width = 250,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 18 + zoom
                    };
                    canvasInfo.Children.Add(tb);
                    Canvas.SetLeft(tb, 2+left+50);
                    Canvas.SetTop(tb, top);
                    tb.Measure(size);
                    top += 10 + tb.DesiredSize.Height;

                    tb = new TextBlock()
                    {
                        Text = FormatIntellisense(obj.summary),
                        Width = 250,
                        TextWrapping  = TextWrapping.Wrap,
                        FontSize = 14 + zoom
                    };
                    canvasInfo.Children.Add(tb);
                    Canvas.SetLeft(tb, 2 + left + 50);
                    Canvas.SetTop(tb, top);
                    tb.Measure(size);
                    top += 10 + tb.DesiredSize.Height;

                    for (int i = 0; i < obj.members.Count; i++)
                    {
                        Member mem = obj.members[i];

                        ImageSource imgSource = null;
                        switch (mem.type)
                        {
                            case MemberTypes.Method:
                                imgSource = ImageSourceFromBitmap(Properties.Resources.IntellisenseMethod);
                                break;
                            case MemberTypes.Property:
                                if (obj.name == "LDColours")
                                {
                                    try
                                    {
                                        System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(50, 50);
                                        System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp);
                                        g.Clear(System.Drawing.Color.FromName(mem.name));
                                        imgSource = ImageSourceFromBitmap(bmp);
                                    }
                                    catch
                                    {
                                        imgSource = ImageSourceFromBitmap(Properties.Resources.IntellisenseProperty);
                                    }
                                }
                                else
                                {
                                    imgSource = ImageSourceFromBitmap(Properties.Resources.IntellisenseProperty);
                                }
                                break;
                            case MemberTypes.Event:
                                imgSource = ImageSourceFromBitmap(Properties.Resources.IntellisenseEvent);
                                break;
                        }
                        img = new Image()
                        {
                            Width = 20,
                            Height = 20,
                            Source = imgSource
                        };
                        canvasInfo.Children.Add(img);
                        Canvas.SetLeft(img, left);
                        Canvas.SetTop(img, top);

                        tb = new TextBlock()
                        {
                            Text = mem.name,
                            Width = 300,
                            TextWrapping = TextWrapping.Wrap,
                            FontSize = 12 + zoom
                        };
                        canvasInfo.Children.Add(tb);
                        Canvas.SetLeft(tb, left+25);
                        Canvas.SetTop(tb, top);
                        tb.Measure(size);
                        top += 10 + tb.DesiredSize.Height;
                        tb.MouseEnter += new MouseEventHandler(methodInfo);
                        if (mem.summary != "")
                        {
                            tb.ToolTip = new TextBlock()
                            {
                                Text = FormatIntellisense(mem.summary),
                                Width = 200,
                                TextWrapping = TextWrapping.Wrap,
                                FontSize = 12 + zoom
                            };
                        }
                    }

                    canvasInfo.MinHeight = top + 20;
                }
                else if (null != member && member != showMemberLast)
                {
                    canvasInfo.Children.Clear();

                    ImageSource imgSource = null;
                    string name = "";
                    switch (member.type)
                    {
                        case MemberTypes.Custom:
                            imgSource = ImageSourceFromBitmap(Properties.Resources.IntellisenseKeyword);
                            name = member.name;
                            break;
                        case MemberTypes.Method:
                            imgSource = ImageSourceFromBitmap(Properties.Resources.IntellisenseMethod);
                            name = member.name;
                            if (member.arguments.Count > 0)
                            {
                                name += "(";
                                for (int i = 0; i < member.arguments.Count; i++)
                                {
                                    name += member.arguments.Keys.ElementAt(i);
                                    if (i < member.arguments.Count - 1) name += ',';
                                }
                                name += ")";
                            }
                            else
                            {
                                name += "()";
                            }
                            break;
                        case MemberTypes.Property:
                            imgSource = ImageSourceFromBitmap(Properties.Resources.IntellisenseProperty);
                            name = member.name;
                            break;
                        case MemberTypes.Event:
                            imgSource = ImageSourceFromBitmap(Properties.Resources.IntellisenseEvent);
                            name = member.name;
                            break;
                    }
                    Image img = new Image()
                    {
                        Width = 50,
                        Height = 50,
                        Source = imgSource
                    };
                    canvasInfo.Children.Add(img);
                    Canvas.SetLeft(img, left);
                    Canvas.SetTop(img, top);

                    TextBlock tb = new TextBlock()
                    {
                        Text = name,
                        Width = 250,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 18 + zoom
                    };
                    canvasInfo.Children.Add(tb);
                    Canvas.SetLeft(tb, 2 + left + 50);
                    Canvas.SetTop(tb, top);
                    tb.Measure(size);
                    top += 10 + tb.DesiredSize.Height;

                    tb = new TextBlock()
                    {
                        Text = FormatIntellisense(member.summary),
                        Width = 250,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 14 + zoom
                    };
                    canvasInfo.Children.Add(tb);
                    Canvas.SetLeft(tb, 2 + left + 50);
                    Canvas.SetTop(tb, top);
                    tb.Measure(size);
                    top += 10 + tb.DesiredSize.Height;

                    if (null != member.arguments && member.arguments.Count > 0)
                    {
                        tb = new TextBlock()
                        {
                            Text = "Arguments",
                            Width = 300,
                            TextWrapping = TextWrapping.Wrap,
                            FontSize = 14 + zoom,
                            FontWeight = FontWeights.Bold
                        };
                        canvasInfo.Children.Add(tb);
                        Canvas.SetLeft(tb, left);
                        Canvas.SetTop(tb, top);
                        tb.Measure(size);
                        top += 10 + tb.DesiredSize.Height;

                        foreach (KeyValuePair<string, string> pair in member.arguments)
                        {
                            tb = new TextBlock()
                            {
                                Text = pair.Key,
                                Width = 300,
                                TextWrapping = TextWrapping.Wrap,
                                FontSize = 14 + zoom,
                                Foreground = new SolidColorBrush(Colors.Crimson)
                            };
                            canvasInfo.Children.Add(tb);
                            Canvas.SetLeft(tb, left);
                            Canvas.SetTop(tb, top);
                            tb.Measure(size);
                            top += 10 + tb.DesiredSize.Height;

                            tb = new TextBlock()
                            {
                                Text = FormatIntellisense(pair.Value),
                                Width = 300,
                                TextWrapping = TextWrapping.Wrap,
                                FontSize = 12 + zoom
                            };
                            canvasInfo.Children.Add(tb);
                            Canvas.SetLeft(tb, left);
                            Canvas.SetTop(tb, top);
                            tb.Measure(size);
                            top += 10 + tb.DesiredSize.Height;
                        }
                    }

                    if (null != member.returns && member.returns.Length > 0)
                    {
                        tb = new TextBlock()
                        {
                            Text = "Returns",
                            Width = 300,
                            TextWrapping = TextWrapping.Wrap,
                            FontSize = 14 + zoom,
                            FontWeight = FontWeights.Bold
                        };
                        canvasInfo.Children.Add(tb);
                        Canvas.SetLeft(tb, left);
                        Canvas.SetTop(tb, top);
                        tb.Measure(size);
                        top += 10 + tb.DesiredSize.Height;

                        tb = new TextBlock()
                        {
                            Text = FormatIntellisense(member.returns),
                            Width = 300,
                            TextWrapping = TextWrapping.Wrap,
                            FontSize = 12 + zoom
                        };
                        canvasInfo.Children.Add(tb);
                        Canvas.SetLeft(tb, left);
                        Canvas.SetTop(tb, top);
                        tb.Measure(size);
                        top += 10 + tb.DesiredSize.Height;
                    }

                    if (null != member.other && member.other.Count > 0)
                    {
                        foreach (KeyValuePair<string, string> pair in member.other)
                        {
                            tb = new TextBlock()
                            {
                                Text = pair.Key,
                                Width = 300,
                                TextWrapping = TextWrapping.Wrap,
                                FontSize = 14 + zoom,
                                FontWeight = FontWeights.Bold
                            };
                            canvasInfo.Children.Add(tb);
                            Canvas.SetLeft(tb, left);
                            Canvas.SetTop(tb, top);
                            tb.Measure(size);
                            top += 10 + tb.DesiredSize.Height;

                            tb = new TextBlock()
                            {
                                Text = FormatIntellisense(pair.Value),
                                Width = 300,
                                TextWrapping = TextWrapping.Wrap,
                                FontSize = 12 + zoom
                            };
                            canvasInfo.Children.Add(tb);
                            Canvas.SetLeft(tb, left);
                            Canvas.SetTop(tb, top);
                            tb.Measure(size);
                            top += 10 + tb.DesiredSize.Height;
                        }
                    }

                    canvasInfo.MinHeight = top + 20;
                }

                showObjectLast = obj;
                showMemberLast = member;
            }
            catch (Exception ex)
            {

            }
        }

        private string FormatIntellisense(string text)
        {
            string result = text;
            while (result.Contains("\n ")) result = result.Replace("\n ", "\n");
            return result;
        }

        private void UpdateFileSeracher()
        {
            if (MarkedForOpen.Count > 0)
            {
                AddDocument();
                string path = MarkedForOpen.Dequeue();
                activeDocument.LoadDataFromFile(path);
                activeTab.Header = new TabHeader(path);
                SetTabHeaderStyle(activeTab);
            }
        }

        private void UpdateStatusBar()
        {
            statusLines.Content = activeDocument.TextArea.Lines.Count + " lines";
            statusPosition.Content = "line " + (activeDocument.TextArea.CurrentLine + 1) + " column " + (activeDocument.TextArea.GetColumn(activeDocument.TextArea.CurrentPosition) + 1);
            statusCaps.Content = Keyboard.IsKeyToggled(Key.CapsLock) ? "Caps Lock" : "";
            statusNumlock.Content = Keyboard.IsKeyToggled(Key.NumLock) ? "Num Lock" : "";
            statusInsert.Content = Keyboard.IsKeyToggled(Key.Insert) ? "Insert" : "";
            if (null == activeDocument.debug) statusRun.Content = "";
            else if (activeDocument.debug.IsDebug()) statusRun.Content = "Debugging " + ((TabHeader)activeTab.Header).FileName;
            else if (!activeDocument.debug.IsDebug()) statusRun.Content = "Running " + ((TabHeader)activeTab.Header).FileName;
        }

        private void UpdateZoom()
        {
            FontSize = 12 + zoom;
            statusBar.FontSize = 12 + zoom;
            ribbon.FontSize = 12 + zoom;
        }

        private void methodInfo(object sender, MouseEventArgs e)
        {
        }

        public static ImageSource ImageSourceFromBitmap(System.Drawing.Bitmap bmp)
        {
            return Imaging.CreateBitmapSourceFromHBitmap(bmp.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }

        public void SetValue(string var, string value)
        {
            if (null == activeDocument.debug || !activeDocument.debug.IsPaused()) return;
            activeDocument.debug.SetValue(var, value);
        }

        private void OpenFindDialog()
        {
            if (activeDocument.TextArea.SelectedText != "") tbFind.Text = activeDocument.TextArea.SelectedText;
            if (!cbFindText.Items.Contains(tbFind.Text)) cbFindText.Items.Insert(0, tbFind.Text);
            cbFind.SelectedItem = tbFind.Text;
            tbFind.Focus();
            tbFind.SelectAll();
            FindNext();
        }

        private void cbFind_SelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            tbFind.Text = e.NewValue.ToString();
            tbFind.Focus();
            tbFind.SelectAll();
            FindNext();
        }

        private void tbFind_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                if (!cbFindText.Items.Contains(tbFind.Text)) cbFindText.Items.Insert(0, tbFind.Text);
                cbFind.SelectedItem = tbFind.Text;
                FindNext();
            }
        }

        private void OpenReplaceDialog()
        {
            if (FindAndReplace.Active) return;

            FindAndReplace far = new FindAndReplace(this);
            far.Show();
        }

        private void Debug(bool bContinue)
        {
            lock (lockRun)
            {
                if (null != activeDocument.debug && !activeDocument.debug.IsDebug())
                {
                    Errors.Add(new Error("Run : " + "Cannot mix debug and non-debug runs"));
                    return;
                }
                if (null != activeDocument.debug && activeDocument.debug.IsRunning())
                {
                    activeDocument.debug.Resume();
                }
                else
                {
                    activeDocument.debug = new SBDebug(this, sbInterop, activeDocument, true);
                    activeDocument.debug.Compile();
                    activeDocument.Proc = activeDocument.debug.Run(bContinue, true);
                }
                debugUpdated = false;
            }
        }

        private void Pause()
        {
            if (null == activeDocument.debug) return;
            activeDocument.debug.Pause();
            debugUpdated = false;
        }

        private void StepOut()
        {
            if (null == activeDocument.debug) return;
            activeDocument.debug.StepOut();
            debugUpdated = false;
        }

        private void StepOver()
        {
            if (null == activeDocument.debug) return;
            activeDocument.debug.StepOver();
            debugUpdated = false;
        }

        private void Step()
        {
            if (null == activeDocument.debug)
            {
                Debug(false);
            }
            else
            {
                activeDocument.debug.Step();
            }
            debugUpdated = false;
        }

        private void Resume()
        {
            if (null == activeDocument.debug) return;
            activeDocument.debug.Resume();
            debugUpdated = false;
        }

        private void ClearBP()
        {
            const uint mask = (1 << SBDocument.BREAKPOINT_MARKER);
            foreach (Line line in activeDocument.TextArea.Lines)
            {
                if ((line.MarkerGet() & mask) > 0)
                {
                    // Remove existing breakpoint
                    line.MarkerDelete(SBDocument.BREAKPOINT_MARKER);
                }
            }
            if (null == activeDocument.debug) return;
            activeDocument.debug.ClearBP();
        }

        private void ToggleBP()
        {
            Line line = activeDocument.TextArea.Lines[activeDocument.TextArea.CurrentLine];
            activeDocument.ToggleBP(line);
        }

        private void ToggleBM()
        {
            Line line = activeDocument.TextArea.Lines[activeDocument.TextArea.CurrentLine];
            activeDocument.ToggleBM(line);
        }

        private void ClearBM()
        {
            const uint mask = (1 << SBDocument.BOOKMARK_MARKER);
            foreach (Line line in activeDocument.TextArea.Lines)
            {
                if ((line.MarkerGet() & mask) > 0)
                {
                    // Remove existing breakpoint
                    line.MarkerDelete(SBDocument.BOOKMARK_MARKER);
                }
            }
        }

        public void NextBM()
        {
            activeDocument.NavForward();
        }

        public void PreviousBM()
        {
            activeDocument.NavBack();
        }

        private void Stop()
        {
            try
            {
                if (null != activeDocument.Proc && !activeDocument.Proc.HasExited)
                {
                    activeDocument.ClearHighlights();
                    Errors.Add(new Error("Run : " + "Successfully terminated run with process " + activeDocument.Proc.Id));
                    activeDocument.Proc.Kill();
                }
                activeDocument.Proc = null;
                if (null == activeDocument.debug) return;
                activeDocument.debug.Dispose();
                activeDocument.debug = null;
            }
            catch
            {

            }
        }

        private void Ignore()
        {
            ignoreBP = !ignoreBP;
            if (null == activeDocument.debug) return;
            activeDocument.debug.Ignore(ignoreBP);
        }

        private void Run()
        {
            lock (lockRun)
            {
                if (null != activeDocument.debug)
                {
                    Errors.Add(new Error("Run : " + "Cannot compile a program that is already running"));
                    return;
                }
                activeDocument.debug = new SBDebug(this, sbInterop, activeDocument, false);
                activeDocument.debug.Compile();
                activeDocument.Proc = activeDocument.debug.Run(true, false);
            }
        }

        private void Kill()
        {
            try
            {
                if (null != activeDocument.Proc && !activeDocument.Proc.HasExited)
                {
                    activeDocument.ClearHighlights();
                    Errors.Add(new Error("Run : " + "Successfully terminated run with process " + activeDocument.Proc.Id));
                    activeDocument.Proc.Kill();
                }
                activeDocument.Proc = null;
                if (null == activeDocument.debug) return;
                activeDocument.debug.Dispose();
                activeDocument.debug = null;
            }
            catch
            {

            }
        }

        private void Format()
        {
            activeDocument.Lexer.Format();
        }

        private void Collapse()
        {
            activeDocument.FoldAll(FoldAction.Contract);
        }

        private void Expand()
        {
            activeDocument.FoldAll(FoldAction.Expand);
        }

        private void Copy()
        {
            activeDocument.Copy();
        }

        private void Paste()
        {
            activeDocument.Paste();
        }

        private void Cut()
        {
            activeDocument.Cut();
        }

        private void Delete()
        {
            activeDocument.Delete();
        }

        private void SelectAll()
        {
            activeDocument.SelectAll();
        }

        private void Undo()
        {
            activeDocument.Undo();
        }

        private void Redo()
        {
            activeDocument.Redo();
        }

        public void Publish()
        {
            string key = sbInterop.Publish(activeDocument.TextArea.Text, ((TabHeader)activeTab.Header).BaseID);
            if (key == "error")
            {
                Errors.Add(new Error("Publish : " + "Failed to publish program (perhaps too short or too long)"));
            }
            else
            {
                ((TabHeader)activeTab.Header).BaseID = key;
                Errors.Add(new Error("Publish : " + "Successfully published program with ID " + key));
                Publish publish = new Publish(sbInterop, key);
                publish.ShowDialog();
            }
        }

        private void Import()
        {
            Import import = new Import(sbInterop);
            import.ShowDialog();
            if (ImportProgram != "error" && ImportProgram != "")
            {
                AddDocument();
                activeDocument.LoadDataFromText(ImportProgram);
                ((TabHeader)activeTab.Header).FilePath = import.textBoxImport.Text;
                ((TabHeader)activeTab.Header).BaseID = import.textBoxImport.Text;
            }
        }

        private void ExtensionManager()
        {
            if (!loadExtensions || MessageBox.Show("Uncheck \"Load extension dlls on startup\" in Advanced->Options and restart to modify installed extensions.\n\nOK to continue with extensions loaded.", "SB-Prime", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
            {
                EMWindow windowEM = new EMWindow(Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, "settings"), InstallDir);
                windowEM.ShowDialog();
                sbInterop = new SBInterop();
            }
        }

        public void FindNext()
        {
            activeDocument.searchManager.Find(true, tbFind.Text);
        }

        private void FindPrevious()
        {
            activeDocument.searchManager.Find(false, tbFind.Text);
        }

        private void DualScreen()
        {
            dualScreen = !dualScreen;
            toggleSplit.IsChecked = dualScreen;
            documentGrid.ColumnDefinitions[1].MaxWidth = dualScreen ? 6 : 0;
            documentGrid.ColumnDefinitions[2].MaxWidth = dualScreen ? double.PositiveInfinity : 0;
            wrapperGrid.Children[4].Visibility = dualScreen ? Visibility.Visible : Visibility.Hidden;
        }

        private void Wrap()
        {
            wrap = !wrap;
            foreach (TabItem tab in tabControlSB2.Items)
            {
                SBDocument doc = (SBDocument)tab.Tag;
                doc.WrapMode = wrap ? WrapMode.Whitespace : WrapMode.None;
            }
            foreach (TabItem tab in tabControlSB1.Items)
            {
                SBDocument doc = (SBDocument)tab.Tag;
                doc.WrapMode = wrap ? WrapMode.Whitespace : WrapMode.None;
            }
        }

        private void Indent()
        {
            indent = !indent;
            foreach (TabItem tab in tabControlSB2.Items)
            {
                SBDocument doc = (SBDocument)tab.Tag;
                doc.IndentationGuides = indent ? IndentView.LookBoth : IndentView.None;
            }
                foreach (TabItem tab in tabControlSB1.Items)
            {
                SBDocument doc = (SBDocument)tab.Tag;
                doc.IndentationGuides = indent ? IndentView.LookBoth : IndentView.None;
            }
        }

        private void Whitespace()
        {
            whitespace = !whitespace;
            foreach (TabItem tab in tabControlSB2.Items)
            {
                SBDocument doc = (SBDocument)tab.Tag;
                doc.ViewWhitespace = whitespace ? WhitespaceMode.VisibleAlways : WhitespaceMode.Invisible;
            }
            foreach (TabItem tab in tabControlSB1.Items)
            {
                SBDocument doc = (SBDocument)tab.Tag;
                doc.ViewWhitespace = whitespace ? WhitespaceMode.VisibleAlways : WhitespaceMode.Invisible;
            }
        }

        private void Print()
        {
            try
            {
                PrintDialog pd = new PrintDialog();
                if ((bool)pd.ShowDialog().GetValueOrDefault())
                {
                    FlowDocument flowDocument = new FlowDocument();
                    flowDocument.PageHeight = pd.PrintableAreaHeight;
                    flowDocument.PageWidth = pd.PrintableAreaWidth;
                    flowDocument.PagePadding = new Thickness(25);

                    flowDocument.ColumnGap = 0;

                    flowDocument.ColumnWidth = (flowDocument.PageWidth -
                        flowDocument.ColumnGap -
                        flowDocument.PagePadding.Left -
                        flowDocument.PagePadding.Right);

                    flowDocument.FontSize = 12;
                    foreach (string line in activeDocument.TextArea.Text.Split('\n'))
                    {
                        Paragraph myParagraph = new Paragraph();
                        myParagraph.Margin = new Thickness(0);
                        myParagraph.Inlines.Add(new Run(line));
                        flowDocument.Blocks.Add(myParagraph);
                    }

                    DocumentPaginator paginator = ((IDocumentPaginatorSource)flowDocument).DocumentPaginator;
                    pd.PrintDocument(paginator, Path.GetFileName(activeDocument.Filepath));
                }
            }
            catch (Exception ex)
            {
                Errors.Add(new Error("Print : " + ex.Message));
            }
        }

        private void ZoomIn()
        {
            activeDocument.ZoomIn();
            zoom = activeDocument.TextArea.Zoom;
            foreach (TabItem tab in tabControlSB2.Items)
            {
                SBDocument doc = (SBDocument)tab.Tag;
                doc.TextArea.Zoom = zoom;
            }
            foreach (TabItem tab in tabControlSB1.Items)
            {
                SBDocument doc = (SBDocument)tab.Tag;
                doc.TextArea.Zoom = zoom;
            }
        }

        private void ZoomOut()
        {
            activeDocument.ZoomOut();
            zoom = activeDocument.TextArea.Zoom;
            foreach (TabItem tab in tabControlSB2.Items)
            {
                SBDocument doc = (SBDocument)tab.Tag;
                doc.TextArea.Zoom = zoom;
            }
            foreach (TabItem tab in tabControlSB1.Items)
            {
                SBDocument doc = (SBDocument)tab.Tag;
                doc.TextArea.Zoom = zoom;
            }
        }

        private void ZoomReset()
        {
            activeDocument.ZoomDefault();
            zoom = activeDocument.TextArea.Zoom;
            foreach (TabItem tab in tabControlSB2.Items)
            {
                SBDocument doc = (SBDocument)tab.Tag;
                doc.TextArea.Zoom = zoom;
            }
            foreach (TabItem tab in tabControlSB1.Items)
            {
                SBDocument doc = (SBDocument)tab.Tag;
                doc.TextArea.Zoom = zoom;
            }
        }

        private void Theme()
        {
            theme = (theme + 1) % 2;
            foreach (TabItem tab in tabControlSB2.Items)
            {
                SBDocument doc = (SBDocument)tab.Tag;
                doc.Theme = theme;
            }
            foreach (TabItem tab in tabControlSB1.Items)
            {
                SBDocument doc = (SBDocument)tab.Tag;
                doc.Theme = theme;
            }
        }

        private void GraduateVB()
        {
            Graduate graduate = new Graduate();
            graduate.ShowDialog();
            if (graduate.OK)
            {
                string tempCode = Path.GetTempFileName();
                File.Delete(tempCode);
                File.WriteAllText(tempCode, activeDocument.TextArea.Text);
                string result = sbInterop.Graduate(tempCode, Path.GetFileNameWithoutExtension(activeDocument.Filepath), Dialogs.Graduate.ProjectPath);
                File.Delete(tempCode);
                if (result != "")
                {
                    Errors.Add(new Error("Graduate : " + "Successfully graduated program to " + result));
                    Process.Start(result);
                }
            }
        }

        public void SetTabHeaderStyle(TabItem tab)
        {
            tab.Style = (System.Windows.Style)FindResource("RoundedTabItem");
        }
    }

    public class TabHeader : Grid
    {
        private string filePath;
        private string fileName;
        private string baseID;
        private TextBlock textBlock = new TextBlock() { FontWeight = FontWeights.Bold, FontSize = 12 + MainWindow.zoom };

        public string FileName
        {
            get { return fileName; }
        }

        public string FilePath
        {
            get { return filePath; }
            set
            {
                filePath = value;
                fileName = Path.GetFileName(filePath);
                ToolTip = new TextBlock() { Text = filePath };
            }
        }

        public string BaseID
        {
            get { return baseID; }
            set { baseID = value; }
        }

        public TabHeader(string _filePath)
        {
            ImageSource imgSource = MainWindow.ImageSourceFromBitmap(Properties.Resources.Erase);
            Image img = new Image()
            {
                Width = 14,
                Height = 14,
                Source = imgSource
            };
            Button button = new Button() { Content = img,
                Background = new SolidColorBrush(Colors.Transparent), BorderBrush = new SolidColorBrush(Colors.Transparent) };

            baseID = "SBProgram";
            filePath = _filePath;
            fileName = Path.GetFileName(filePath);
            Children.Add(textBlock);
            Children.Add(button);
            VerticalAlignment = VerticalAlignment.Center;
            ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength() });
            ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength() });
            SetColumn(textBlock, 0);
            SetColumn(button, 1);
            textBlock.Text = fileName + " ";
            button.Click += new RoutedEventHandler(OnClick);
            ToolTip = new TextBlock() { Text = filePath };
        }

        public void SetDirty(bool isDirty)
        {
            if (isDirty) textBlock.Text = fileName + " * ";
            else textBlock.Text = fileName + " ";
        }

        private void OnClick(Object sender, RoutedEventArgs e)
        {
            MainWindow.MarkedForDelete.Enqueue((TabItem)Parent);
        }
    }

    public class DebugData : INotifyPropertyChanged
    {
        public string _Variable { get; set; }
        public string _Value { get; set; }
        public string _LessThan { get; set; }
        public string _GreaterThan { get; set; }
        public string _Equal { get; set; }
        public bool _Changes { get; set; }

        public string Variable
        {
            get { return _Variable; }
            set
            {
                if (value != _Variable)
                {
                    _Variable = value;
                    NotifyPropertyChanged("Variable");
                }
            }
        }

        public string Value
        {
            get { return _Value; }
            set
            {
                if (value != _Value)
                {
                    _Value = value;
                    NotifyPropertyChanged("Value");
                }
            }
        }

        public string LessThan
        {
            get { return _LessThan; }
            set
            {
                if (value != _LessThan)
                {
                    _LessThan = value;
                    NotifyPropertyChanged("LessThan");
                }
            }
        }

        public string GreaterThan
        {
            get { return _GreaterThan; }
            set
            {
                if (value != _GreaterThan)
                {
                    _GreaterThan = value;
                    NotifyPropertyChanged("GreaterThan");
                }
            }
        }

        public string Equal
        {
            get { return _Equal; }
            set
            {
                if (value != _Equal)
                {
                    _Equal = value;
                    NotifyPropertyChanged("Equal");
                }
            }
        }

        public bool Changes
        {
            get { return _Changes; }
            set
            {
                if (value != _Changes)
                {
                    _Changes = value;
                    NotifyPropertyChanged("Changes");
                }
            }
        }

        public DebugData()
        {
            Variable = "";
            Value = "";
            LessThan = "";
            GreaterThan = "";
            Equal = "";
            Changes = false;
        }

        public string Group
        {
            get
            {
                return Variable + "?" + LessThan + "?" + GreaterThan + "?" + Equal + "?" + Changes;
            }
            set
            {
                string[] bits = value.Split('?');
                int i = 0;
                if (i < bits.Length) Variable = bits[i++];
                if (i < bits.Length) LessThan = bits[i++];
                if (i < bits.Length) GreaterThan = bits[i++];
                if (i < bits.Length) Equal = bits[i++];
                if (i < bits.Length) Changes = bits[i++] == true.ToString();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] String info = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }

        public void ClearWatch()
        {
            LessThan = "";
            GreaterThan = "";
            Equal = "";
            Changes = false;
        }
    }

    public class Error
    {
        public string Message { get; set; }
        public int Row { get; set; }
        public int Col { get; set; }
        public int Level { get; set; }

        public Error(string Message)
        {
            this.Message = Message;
            Row = -1;
            Col = -1;
            Level = 0;
        }
    }
}
