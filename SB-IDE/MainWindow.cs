using ExtensionManagerLibrary;
using SB_IDE.Dialogs;
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

namespace SB_IDE
{
    public partial class MainWindow
    {
        public static List<Error> Errors = new List<Error>();
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
        public static int zoom = 0;
        public static int theme = 0;
        public static Size size = new Size(double.PositiveInfinity, double.PositiveInfinity);
        public static bool CompileError = false;
        public static Queue<TabItem> MarkedForDelete = new Queue<TabItem>();
        public static Queue<string> MarkedForOpen = new Queue<string>();
        public static Queue<string> MarkedForWatch = new Queue<string>();

        public List<DebugData> debugData = new List<DebugData>();
        SBInterop sbInterop;
        SBplugin sbPlugin;
        SBDocument activeDocument;
        TabItem activeTab;
        Timer threadTimer;
        bool debugUpdated = false;
        int maxMRU = 10;

        private void InitWindow()
        {
            statusVersion.Content = "SB-IDE Version 1.0." + SBInterop.CurrentVersion;
            Errors.Add(new Error("Welcome to Small Basic IDE"));

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
            toggleTheme.IsChecked = theme > 0;
            viewLanguage.Text = SBInterop.Language;

            // DEFAULT FILE
            AddDocument(1);
            AddDocument(2);
            tabControlSB1.Focus();
            App app = (App)Application.Current;
            for (int i = 0; i < app.Arguments.Length; i++)
            {
                if (i == 0)
                {
                    activeDocument.LoadDataFromFile(app.Arguments[i]);
                    activeTab.Header = new TabHeader(app.Arguments[i]);
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

            InitIntellisense();

            SetWindowColors();

            threadTimer = new Timer(new TimerCallback(ThreadTimerCallback));
            threadTimer.Change(100, 100);
        }

        public SBDocument GetActiveDocument()
        {
            return activeDocument;
        }

        private void SetWindowColors()
        {
            Resources["GridBrushBackground"] = new SolidColorBrush(IntToColor(BACKGROUND_COLOR));
            Resources["GridBrushForeground"] = new SolidColorBrush(IntToColor(FOREGROUND_COLOR));
            Resources["SplitterBrush"] = new SolidColorBrush(IntToColor(SPLITTER_COLOR));
        }

        private void InitIntellisense()
        {
            canvasInfo.Children.Clear();
            TextBlock tb = new TextBlock()
            {
                Text = "Intellisense",
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 24
            };
            canvasInfo.Children.Add(tb);
            tb.Measure(size);
            double canvasWidth = Math.Max(canvasInfo.ActualWidth, Math.Max(20 + tb.DesiredSize.Width, 200));
            Canvas.SetLeft(tb, (canvasWidth - tb.DesiredSize.Width) / 2);
            Canvas.SetTop(tb, 25);

            tb = new TextBlock()
            {
                Text = "Intellisense will appear here when you hover over objects and methods as well as when you type and view potential options.\n\n" +
                "Additionally, a popup description for methods can be viewed in this window by hovering the mouse over methods, properties or events when viewing an object.",
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                FontSize = 14,
                Width = (canvasWidth - 50)
            };
            canvasInfo.Children.Add(tb);
            Canvas.SetLeft(tb, 25);
            Canvas.SetTop(tb, 75);

            canvasInfo.MinHeight = 100 + tb.DesiredSize.Height;
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
            dataGridDebug.Items.Refresh();
        }

        private void GridResultsClick(object sender, RoutedEventArgs e)
        {
            Errors.Clear();
            dataGridResults.Items.Refresh();
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

            int i = 0;
            if (Properties.Settings.Default.MRU.Count > i) MRU1.Content = Ellipsis(Properties.Settings.Default.MRU[i++]);
            if (Properties.Settings.Default.MRU.Count > i) MRU2.Content = Ellipsis(Properties.Settings.Default.MRU[i++]);
            if (Properties.Settings.Default.MRU.Count > i) MRU3.Content = Ellipsis(Properties.Settings.Default.MRU[i++]);
            if (Properties.Settings.Default.MRU.Count > i) MRU4.Content = Ellipsis(Properties.Settings.Default.MRU[i++]);
            if (Properties.Settings.Default.MRU.Count > i) MRU5.Content = Ellipsis(Properties.Settings.Default.MRU[i++]);
            if (Properties.Settings.Default.MRU.Count > i) MRU6.Content = Ellipsis(Properties.Settings.Default.MRU[i++]);
            if (Properties.Settings.Default.MRU.Count > i) MRU7.Content = Ellipsis(Properties.Settings.Default.MRU[i++]);
            if (Properties.Settings.Default.MRU.Count > i) MRU8.Content = Ellipsis(Properties.Settings.Default.MRU[i++]);
            if (Properties.Settings.Default.MRU.Count > i) MRU9.Content = Ellipsis(Properties.Settings.Default.MRU[i++]);
            if (Properties.Settings.Default.MRU.Count > i) MRU10.Content = Ellipsis(Properties.Settings.Default.MRU[i++]);

            dualScreen = Properties.Settings.Default.SplitScreen;
            wrap = Properties.Settings.Default.WordWrap;
            indent = Properties.Settings.Default.IndentGuides;
            whitespace = Properties.Settings.Default.WhiteSpace;
            zoom = Properties.Settings.Default.Zoom;
            theme = Properties.Settings.Default.Theme;
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
            mainGrid.RowDefinitions[2].Height = new GridLength(Properties.Settings.Default.OutputHeight > 0 ? Properties.Settings.Default.OutputHeight : 150);
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
        }

        private void ResetSettings()
        {
            Properties.Settings.Default.Reset();
            Properties.Settings.Default.Save();
            LoadSettings();
        }

        private Grid Ellipsis(string txt)
        {
            TextBlock tb = new TextBlock() { Text = txt, FontSize = 14, FontWeight = FontWeights.DemiBold };
            tb.ToolTip = txt;
            tb.Text = Path.GetFileName(txt);

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
            for (int i = Properties.Settings.Default.MRU.Count - 1; i >= maxMRU; i--) Properties.Settings.Default.MRU.RemoveAt(i);

            Properties.Settings.Default.SplitScreen = dualScreen;
            Properties.Settings.Default.WordWrap = wrap;
            Properties.Settings.Default.IndentGuides = indent;
            Properties.Settings.Default.WhiteSpace = whitespace;
            Properties.Settings.Default.Zoom = zoom;
            Properties.Settings.Default.Theme = theme;
            Properties.Settings.Default.Language = SBInterop.Language;
            Properties.Settings.Default.Version = SBInterop.Version;
            Properties.Settings.Default.WatchList.Clear();
            for (int i = 0; i < debugData.Count; i++)
            {
                Properties.Settings.Default.WatchList.Add(debugData[i].Group);
            }
            Properties.Settings.Default.RootPath = FileSearcher.RootPath;
            Properties.Settings.Default.OutputHeight = mainGrid.RowDefinitions[2].ActualHeight;
            Properties.Settings.Default.Colors.Clear();
            foreach (KeyValuePair<string,int> kvp in IDEColors)
            {
                Properties.Settings.Default.Colors.Add(kvp.Key + "?" + kvp.Value);
            }

            Properties.Settings.Default.Save();
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
            saveFileDialog.Filter = "Small Basic files (*.sb)|*.sb|All files (*.*)|*.*";
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.RestoreDirectory = true;
            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                activeDocument.SaveDataToFile(saveFileDialog.FileName);
                activeTab.Header = new TabHeader(saveFileDialog.FileName);
                if (Properties.Settings.Default.MRU.Contains(activeDocument.Filepath)) Properties.Settings.Default.MRU.Remove(activeDocument.Filepath);
                if (File.Exists(activeDocument.Filepath)) Properties.Settings.Default.MRU.Insert(0, activeDocument.Filepath);
            }
        }

        private void AddDocument(int iTab = -1)
        {
            int num = 1;
            foreach (TabItem tabItem in tabControlSB1.Items)
            {
                if (((TabHeader)tabItem.Header).FileName.StartsWith("Untitled"))
                {
                    int i;
                    int.TryParse(((TabHeader)tabItem.Header).FileName.Substring(8), out i);
                    if (num == i) num++;
                }
            }
            foreach (TabItem tabItem in tabControlSB2.Items)
            {
                if (((TabHeader)tabItem.Header).FileName.StartsWith("Untitled"))
                {
                    int i;
                    int.TryParse(((TabHeader)tabItem.Header).FileName.Substring(8), out i);
                    if (num == i) num++;
                }
            }
            WindowsFormsHost host = new WindowsFormsHost();
            System.Windows.Forms.Panel panel = new System.Windows.Forms.Panel();
            activeDocument = new SBDocument();
            panel.Contains(activeDocument.TextArea);
            host.Child = activeDocument.TextArea;
            GetTabContol(iTab).Items.Add(new TabItem());
            activeTab = (TabItem)GetTabContol(iTab).Items[GetTabContol(iTab).Items.Count - 1];
            activeTab.Content = host;
            activeTab.Header = new TabHeader("Untitled" + num);
            activeTab.Tag = activeDocument;
            activeDocument.Tab = activeTab;
            activeDocument.TextArea.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(OnPreviewKeyDown);
            activeDocument.WrapMode = wrap ? WrapMode.Whitespace : WrapMode.None;
            activeDocument.IndentationGuides = indent ? IndentView.LookBoth : IndentView.None;
            activeDocument.ViewWhitespace = whitespace ? WhitespaceMode.VisibleAlways : WhitespaceMode.Invisible;
            activeDocument.TextArea.Zoom = zoom;
            activeDocument.Theme = theme;

            GetTabContol(iTab).SelectedIndex = GetTabContol(iTab).Items.Count - 1;
            activeTab.Focus();
        }

        private void OnPreviewKeyDown(object sender, System.Windows.Forms.PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == System.Windows.Forms.Keys.F && e.Modifiers == System.Windows.Forms.Keys.Control)
            {
                OpenFindDialog();
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.H && e.Modifiers == System.Windows.Forms.Keys.Control)
            {
                OpenReplaceDialog();
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.F3 && !e.Shift)
            {
                FindNext();
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.F3 && e.Shift)
            {
                FindPrevious();
            }
        }

        private System.Windows.Forms.DialogResult DeleteDocument()
        {
            if (null != activeDocument.debug) activeDocument.debug.Dispose();

            if (activeDocument.IsDirty)
            {
                System.Windows.Forms.DialogResult dlg = System.Windows.Forms.MessageBox.Show("The text in " + ((TabHeader)activeTab.Header).FileName + " has changed.\n\nDo you want to save the changes?", "SBIDE", System.Windows.Forms.MessageBoxButtons.YesNoCancel, System.Windows.Forms.MessageBoxIcon.Question);
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
                    });
                }
            }
            catch (Exception ex)
            {

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
                    TabItem curTab = activeTab;
                    activeTab = MarkedForDelete.Dequeue();
                    activeDocument = GetDocument();
                    DeleteDocument();
                    if (null != curTab && null != curTab.Parent)
                    {
                        activeTab = curTab;
                        activeDocument = GetDocument();
                    }
                }
            }
        }

        private void UpdateDebug()
        {
            if (MarkedForWatch.Count > 0)
            {
                debugData.Add(new DebugData() { Variable = MarkedForWatch.Dequeue() });
                dataGridDebug.Items.Refresh();
            }
            if (null == activeDocument.debug || !activeDocument.debug.IsPaused())
            {
                debugUpdated = false;
                return;
            }

            if (!debugUpdated)
            {
                activeDocument.debug.ClearConditions();
                foreach (DebugData data in debugData)
                {
                    activeDocument.debug.GetValue(data.Variable);
                    activeDocument.debug.SetCondition(data);
                }
                debugUpdated = true;
            }
        }

        private void UpdateOutput()
        {
            if ((int)dataGridResults.Tag != dataGridResults.Items.Count)
            {
                dataGridResults.Items.Refresh();
                dataGridResults.Tag = dataGridResults.Items.Count;
                dataGridResults.ScrollIntoView(dataGridResults.Items[dataGridResults.Items.Count-1]);
            }
            if (CompileError)
            {
                tabControlResults.SelectedItem = tabOutput;
                CompileError = false;
            }
        }

        private void UpdateRun()
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
                        FontSize = 18
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
                        FontSize = 14
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
                            FontSize = 12
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
                                FontSize = 12
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
                        FontSize = 18
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
                        FontSize = 14
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
                            FontSize = 14,
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
                                FontSize = 14,
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
                                FontSize = 12
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
                            FontSize = 14,
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
                            FontSize = 12
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
                                FontSize = 14,
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
                                FontSize = 12
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
            }
        }

        private void UpdateStatusBar()
        {
            statusLines.Content = activeDocument.TextArea.Lines.Count + " lines";
            statusCaps.Content = Keyboard.IsKeyToggled(Key.CapsLock) ? "Caps Lock" : "";
            statusInsert.Content = Keyboard.IsKeyToggled(Key.Insert) ? "Insert" : "";
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

        public void RefreshDebugData()
        {
            dataGridDebug.Items.Refresh();
        }

        private void OpenFindDialog()
        {
            if (activeDocument.TextArea.SelectedText != "") tbFind.Text = activeDocument.TextArea.SelectedText;
            tbFind.Focus();
            tbFind.SelectAll();
            FindNext();
        }

        private void OpenReplaceDialog()
        {
            FindAndReplace far = new FindAndReplace(activeDocument);
            far.Show();
        }

        private void Debug(bool bContinue)
        {
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

        private void Ignore()
        {
            ignoreBP = !ignoreBP;
            if (null == activeDocument.debug) return;
            activeDocument.debug.Ignore(ignoreBP);
        }

        private void Run()
        {
            activeDocument.debug = new SBDebug(this, sbInterop, activeDocument, false);
            activeDocument.debug.Compile();
            activeDocument.Proc = activeDocument.debug.Run(true, false);
        }

        private void Kill()
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

        private void Publish()
        {
            string key = sbInterop.Publish(activeDocument.TextArea.Text);
            if (key == "error")
            {
                Errors.Add(new Error("Publish : " + "Failed to publish program (perhaps too short or too long)"));
            }
            else
            {
                Errors.Add(new Error("Publish : " + "Successfully published program with ID " + key));
                Publish publish = new Publish(key);
                publish.ShowDialog();
            }
        }

        private void Import()
        {
            Import import = new Import(sbInterop);
            import.ShowDialog();
            if (ImportProgram != "")
            {
                AddDocument();
                activeDocument.LoadDataFromText(ImportProgram);
            }
        }

        private void ExtensionManager()
        {
            EMWindow windowEM = new EMWindow(Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, "settings"));
            windowEM.ShowDialog();
        }

        private void FindNext()
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
            documentGrid.ColumnDefinitions[1].MaxWidth = dualScreen ? 6 : 0;
            documentGrid.ColumnDefinitions[2].MaxWidth = dualScreen ? double.PositiveInfinity : 0;
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
    }

    public class TabHeader : Grid
    {
        public string FilePath;
        public string FileName;
        public TextBlock textBlock = new TextBlock() { FontWeight = FontWeights.Bold, FontSize = 12 };

        public TabHeader(string filePath)
        {
            ImageSource imgSource = MainWindow.ImageSourceFromBitmap(Properties.Resources.Erase);
            Image img = new Image()
            {
                Width = 14,
                Height = 14,
                Source = imgSource
            };
            Button button = new Button() { Content = img, Background = new SolidColorBrush(Colors.Transparent), BorderBrush = new SolidColorBrush(Colors.Transparent) };

            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            Children.Add(textBlock);
            Children.Add(button);
            VerticalAlignment = VerticalAlignment.Center;
            ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength() });
            ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength() });
            SetColumn(textBlock, 0);
            SetColumn(button, 1);
            textBlock.Text = FileName + " ";
            button.Click += new RoutedEventHandler(OnClick);
            ToolTip = new TextBlock() { Text = filePath };
        }

        public void SetDirty(bool isDirty)
        {
            if (isDirty) textBlock.Text = FileName + " * ";
            else textBlock.Text = FileName + " ";
        }

        private void OnClick(Object sender, RoutedEventArgs e)
        {
            MainWindow.MarkedForDelete.Enqueue((TabItem)Parent);
        }
    }

    public class DebugData
    {
        public string Variable { get; set; }
        public string Value { get; set; }
        public string LessThan { get; set; }
        public string GreaterThan { get; set; }
        public string Equal { get; set; }
        public bool Changes { get; set; }

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

        public Error(string Message)
        {
            this.Message = Message;
            Row = -1;
            Col = -1;
        }
    }
}
