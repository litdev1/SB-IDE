using SB_Prime.Dialogs;
using ScintillaNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SB_Prime
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Main Window Colors
        public static int BACKGROUND_COLOR = 0xDFE9F5;
        public static int FOREGROUND_COLOR = 0x000000;
        public static int SPLITTER_COLOR = 0x96AFFF;
        public static int SELECTION_COLOR = 0xC0D0FF;
        // Text Area Colors
        public static int BACK_MARGIN_COLOR = 0xF0F0F0;
        public static int FORE_MARGIN_COLOR = 0x5C5C5C;
        public static int BACK_FOLDING_COLOR = 0xF8F8F8;
        public static int FORE_FOLDING_COLOR = 0x5C5C5C;
        public static int BACK_BOOKMARK_COLOR = 0x5050A0;
        public static int FORE_BOOKMARK_COLOR = 0x5050A0;
        public static int BACK_BREAKPOINT_COLOR = 0xFF003B;
        public static int FORE_BREAKPOINT_COLOR = 0xFF003B;
        public static int SELECT_COLOR = 0xCCDDFF;
        public static int DEBUG_HIGHLIGHT_COLOR = 0xFFFF50;
        public static int FIND_HIGHLIGHT_COLOR = 0xFF0000;
        public static int DELETED_HIGHLIGHT_COLOR = 0xFF8080;
        public static int INSERTED_HIGHLIGHT_COLOR = 0x80FF80;
        public static int FORE_CALLTIP_COLOR = 0x5C5C5C;
        public static int BACK_CALLTIP_COLOR = 0xFFFFFF;
        public static int HIGHLIGHT_CALLTIP_COLOR = 0xFF0000;
        // Lexer Colors
        public static int FORE_COLOR = 0x000000;
        public static int BACK_COLOR = 0xFFFFFF;
        public static int COMMENT_COLOR = 0x008020;
        public static int STRING_COLOR = 0xCC6633;
        public static int OPERATOR_COLOR = 0x800000;
        public static int KEYWORD_COLOR = 0x7777FF;
        public static int OBJECT_COLOR = 0x006060;
        public static int METHOD_COLOR = 0x802020;
        public static int LITERAL_COLOR = 0xDD6633;
        // FlowChart Colors
        public static int CHART_FORE_COLOR = 0x000000;
        public static int CHART_BACK_COLOR = 0xFFFFFF;
        public static int CHART_HIGHLIGHT_COLOR = 0xFFD700;
        public static int CHART_CONDITION_COLOR = 0xFF0000;
        public static int CHART_START_COLOR = 0x008000;
        public static int CHART_CALL_COLOR = 0xFF8C00;
        public static int CHART_FOR_COLOR = 0x008B8B;
        public static int CHART_WHILE_COLOR = 0xFF1493;
        public static int CHART_STATEMENT_COLOR = 0x0000FF;
        public static int CHART_CODE_COLOR = 0xFFFFFF;

        public Dictionary<string, int> DefaultColors { get; }
        public StackVariables stackVariables = null;
        public string exeFolder;

        SplashScreen splashScreen;

        private List<MRUdata> MRUlst = new List<MRUdata>();

        public class MRUdata
        {
            public Grid Ellipsis { get; set; }
        }

        public MainWindow()
        {
            App app = (App)Application.Current;
            for (int i = 0; i < app.Arguments.Length; i++)
            {
                if (app.Arguments[i].StartsWith("/l"))
                {
                    string lang = app.Arguments[i].Substring(app.Arguments[i].IndexOf(":") + 1);
                    Properties.Strings.Culture = new CultureInfo(lang);
                }
            }

            splashScreen = new SplashScreen("Images/splash.png");
            splashScreen.Show(false);

            DefaultColors = IDEColors;

            InitializeComponent();

            MRUlist.ItemsSource = MRUlst;
        }

        public Dictionary<string, int> IDEColors
        {
            get
            {
                Dictionary<string, int> colors = new Dictionary<string, int>();

                colors["W:Background"] = BACKGROUND_COLOR;
                colors["W:Foreground"] = FOREGROUND_COLOR;
                colors["W:Splitter"] = SPLITTER_COLOR;
                colors["W:Selection"] = SELECTION_COLOR;

                colors["D:Margin Background"] = BACK_MARGIN_COLOR;
                colors["D:Margin Foreground"] = FORE_MARGIN_COLOR;
                colors["D:Folding Background"] = BACK_FOLDING_COLOR;
                colors["D:Folding Foreground"] = FORE_FOLDING_COLOR;
                colors["D:Bookmark Background"] = BACK_BOOKMARK_COLOR;
                colors["D:Bookmark Foreground"] = FORE_BOOKMARK_COLOR;
                colors["D:Breakpoint Background"] = BACK_BREAKPOINT_COLOR;
                colors["D:Breakpoint Foreground"] = FORE_BREAKPOINT_COLOR;
                colors["D:Select"] = SELECT_COLOR;
                colors["D:Highlight Debug"] = DEBUG_HIGHLIGHT_COLOR;
                colors["D:Highlight Find"] = FIND_HIGHLIGHT_COLOR;
                colors["D:Highlight Deleted"] = DELETED_HIGHLIGHT_COLOR;
                colors["D:Highlight Inserted"] = INSERTED_HIGHLIGHT_COLOR;
                colors["D:CallTip Foreground"] = FORE_CALLTIP_COLOR;
                colors["D:CallTip Background"] = BACK_CALLTIP_COLOR;
                colors["D:CallTip Highlight"] = HIGHLIGHT_CALLTIP_COLOR;

                colors["L:Foreground"] = FORE_COLOR;
                colors["L:Background"] = BACK_COLOR;
                colors["L:Comment"] = COMMENT_COLOR;
                colors["L:String"] = STRING_COLOR;
                colors["L:Operator"] = OPERATOR_COLOR;
                colors["L:Keyword"] = KEYWORD_COLOR;
                colors["L:Object"] = OBJECT_COLOR;
                colors["L:Method"] = METHOD_COLOR;
                colors["L:Literal"] = LITERAL_COLOR;

                colors["C:Foreground"] = CHART_FORE_COLOR;
                colors["C:Background"] = CHART_BACK_COLOR;
                colors["C:Highlight"] = CHART_HIGHLIGHT_COLOR;
                colors["C:Condition"] = CHART_CONDITION_COLOR;
                colors["C:Start"] = CHART_START_COLOR;
                colors["C:Call"] = CHART_CALL_COLOR;
                colors["C:For"] = CHART_FOR_COLOR;
                colors["C:While"] = CHART_WHILE_COLOR;
                colors["C:Statement"] = CHART_STATEMENT_COLOR;
                colors["C:Code"] = CHART_CODE_COLOR;

                return colors;
            }
            set
            {
                Dictionary<string, int> colors = value;

                BACKGROUND_COLOR = colors["W:Background"];
                FOREGROUND_COLOR = colors["W:Foreground"];
                SPLITTER_COLOR = colors["W:Splitter"];
                SELECTION_COLOR = colors["W:Selection"];

                BACK_MARGIN_COLOR = colors["D:Margin Background"];
                FORE_MARGIN_COLOR = colors["D:Margin Foreground"];
                BACK_FOLDING_COLOR = colors["D:Folding Background"];
                FORE_FOLDING_COLOR = colors["D:Folding Foreground"];
                BACK_BOOKMARK_COLOR = colors["D:Bookmark Background"];
                FORE_BOOKMARK_COLOR = colors["D:Bookmark Foreground"];
                BACK_BREAKPOINT_COLOR = colors["D:Breakpoint Background"];
                FORE_BREAKPOINT_COLOR = colors["D:Breakpoint Foreground"];
                SELECT_COLOR = colors["D:Select"];
                DEBUG_HIGHLIGHT_COLOR = colors["D:Highlight Debug"];
                FIND_HIGHLIGHT_COLOR = colors["D:Highlight Find"];
                DELETED_HIGHLIGHT_COLOR = colors["D:Highlight Deleted"];
                INSERTED_HIGHLIGHT_COLOR = colors["D:Highlight Inserted"];
                FORE_CALLTIP_COLOR = colors["D:CallTip Foreground"];
                BACK_CALLTIP_COLOR = colors["D:CallTip Background"];
                HIGHLIGHT_CALLTIP_COLOR = colors["D:CallTip Highlight"];

                FORE_COLOR = colors["L:Foreground"];
                BACK_COLOR = colors["L:Background"];
                COMMENT_COLOR = colors["L:Comment"];
                STRING_COLOR = colors["L:String"];
                OPERATOR_COLOR = colors["L:Operator"];
                KEYWORD_COLOR = colors["L:Keyword"];
                OBJECT_COLOR = colors["L:Object"];
                METHOD_COLOR = colors["L:Method"];
                LITERAL_COLOR = colors["L:Literal"];

                CHART_FORE_COLOR = colors["C:Foreground"];
                CHART_BACK_COLOR = colors["C:Background"];
                CHART_HIGHLIGHT_COLOR = colors["C:Highlight"];
                CHART_CONDITION_COLOR = colors["C:Condition"];
                CHART_START_COLOR = colors["C:Start"];
                CHART_CALL_COLOR = colors["C:Call"];
                CHART_FOR_COLOR = colors["C:For"];
                CHART_WHILE_COLOR = colors["C:While"];
                CHART_STATEMENT_COLOR = colors["C:Statement"];
                CHART_CODE_COLOR = colors["C:Code"];
            }
        }

        public static Color IntToColor(int rgb)
        {
            return Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            exeFolder = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (Properties.Settings.Default.UpgradeRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.Save();
            }
            LoadSettings();

            string update = exeFolder + "\\Update.exe-";
            if (File.Exists(update))
            {
                File.Copy(update, exeFolder + "\\Update.exe", true);
                File.Delete(update);
            }

            InitWindow();

            splashScreen.Close(new TimeSpan(0, 0, 1));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitIntellisense();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();

            e.Cancel = CloseTab(tabControlSB1);
            if (e.Cancel) return;

            e.Cancel = CloseTab(tabControlSB2);
            if (e.Cancel) return;

            foreach (Window item in App.Current.Windows)
            {
                if (item != this) item.Close();
            }
        }

        private void fileNew_Click(object sender, RoutedEventArgs e)
        {
            AddDocument();
        }

        private void FileOpen()
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Small Basic files (*.sb)|*.sb|All files (*.*)|*.*";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = openFileDialog.FileName;
                AddDocument();
                activeDocument.LoadDataFromFile(path);
                activeTab.Header = new TabHeader(path);
                SetTabHeaderStyle(activeTab);
            }
        }

        private void fileOpen_Click(object sender, RoutedEventArgs e)
        {
            FileOpen();
        }

        private void fileClose_Click(object sender, RoutedEventArgs e)
        {
            DeleteDocument();
        }

        private void FileSave()
        {
            if (activeDocument.IsDirty)
            {
                if (activeDocument.Filepath != "")
                {
                    activeDocument.SaveDataToFile();
                }
                else
                {
                    SaveDocumentAs();
                }
            }
        }

        private void fileSave_Click(object sender, RoutedEventArgs e)
        {
            FileSave();
        }

        private void fileSaveAs_Click(object sender, RoutedEventArgs e)
        {
            SaveDocumentAs();
        }

        private void fileSaveAll_Click(object sender, RoutedEventArgs e)
        {
            TabItem curTab = activeTab;

            for (int i = 0; i < tabControlSB1.Items.Count; i++)
            {
                tabControlSB1.SelectedIndex = i;
                activeTab = (TabItem)tabControlSB1.Items[i];
                activeDocument = GetDocument();
                if (activeDocument.IsDirty) SaveDocumentAs();
            }

            for (int i = 0; i < tabControlSB2.Items.Count; i++)
            {
                tabControlSB2.SelectedIndex = i;
                activeTab = (TabItem)tabControlSB2.Items[i];
                activeDocument = GetDocument();
                if (activeDocument.IsDirty) SaveDocumentAs();
            }

            activeTab = curTab;
            activeDocument = GetDocument();
        }

        private void tabControlSB1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Activate((TabControl)sender);
        }

        private void tabControlSB2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Activate((TabControl)sender);
        }

        private void tabControlSB1_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Activate((TabControl)sender);
        }

        private void tabControlSB2_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Activate((TabControl)sender);
        }

        private void tabControlSB1_GotFocus(object sender, RoutedEventArgs e)
        {
            Activate((TabControl)sender);
        }

        private void tabControlSB2_GotFocus(object sender, RoutedEventArgs e)
        {
            Activate((TabControl)sender);
        }

        private void debugDebug_Click(object sender, RoutedEventArgs e)
        {
            Debug(true);
        }

        private void debugPause_Click(object sender, RoutedEventArgs e)
        {
            Pause();
        }

        private void breakpointClear_Click(object sender, RoutedEventArgs e)
        {
            ClearBP();
        }

        private void debugStop_Click(object sender, RoutedEventArgs e)
        {
            Stop();
        }

        private void debugStep_Click(object sender, RoutedEventArgs e)
        {
            Step();
        }

        private void debugStepOver_Click(object sender, RoutedEventArgs e)
        {
            StepOver();
        }

        private void debugStepOut_Click(object sender, RoutedEventArgs e)
        {
            StepOut();
        }

        private void breakpointIgnore_Click(object sender, RoutedEventArgs e)
        {
            Ignore();
        }

        private void programRun_Click(object sender, RoutedEventArgs e)
        {
            Run();
        }

        private void programStop_Click(object sender, RoutedEventArgs e)
        {
            Kill();
        }

        private void breakpointToggle_Click(object sender, RoutedEventArgs e)
        {
            ToggleBP();
        }

        private void bookmarkToggle_Click(object sender, RoutedEventArgs e)
        {
            ToggleBM();
        }

        private void bookmarkClear_Click(object sender, RoutedEventArgs e)
        {
            ClearBM();
        }

        private void bookmarkNext_Click(object sender, RoutedEventArgs e)
        {
            NextBM();
        }

        private void bookmarkPrevious_Click(object sender, RoutedEventArgs e)
        {
            PreviousBM();
        }

        private void editCollapse_Click(object sender, RoutedEventArgs e)
        {
            Collapse();
        }

        private void editExpand_Click(object sender, RoutedEventArgs e)
        {
            Expand();
        }

        private void editCopy_Click(object sender, RoutedEventArgs e)
        {
            Copy();
        }

        private void editPaste_Click(object sender, RoutedEventArgs e)
        {
            Paste();
        }

        private void editCut_Click(object sender, RoutedEventArgs e)
        {
            Cut();
        }

        private void editDelete_Click(object sender, RoutedEventArgs e)
        {
            Delete();
        }

        private void editSelectAll_Click(object sender, RoutedEventArgs e)
        {
            SelectAll();
        }

        private void editUndo_Click(object sender, RoutedEventArgs e)
        {
            Undo();
        }

        private void editRedo_Click(object sender, RoutedEventArgs e)
        {
            Redo();
        }

        private void editFormat_Click(object sender, RoutedEventArgs e)
        {
            Format();
        }

        private void webPublish_Click(object sender, RoutedEventArgs e)
        {
            Publish();
        }

        private void webImport_Click(object sender, RoutedEventArgs e)
        {
            Import();
        }

        private void extensionsManage_Click(object sender, RoutedEventArgs e)
        {
            ExtensionManager();
        }

        private void searchNext_Click(object sender, RoutedEventArgs e)
        {
            FindNext();
        }

        private void searchPrevious_Click(object sender, RoutedEventArgs e)
        {
            FindPrevious();
        }

        private void viewDual_Click(object sender, RoutedEventArgs e)
        {
            DualScreen();
        }

        private void searchReplace_Click(object sender, RoutedEventArgs e)
        {
            OpenReplaceDialog();
        }

        private void viewWrap_Click(object sender, RoutedEventArgs e)
        {
            Wrap();
        }

        private void viewIndent_Click(object sender, RoutedEventArgs e)
        {
            Indent();
        }

        private void viewWhitespace_Click(object sender, RoutedEventArgs e)
        {
            Whitespace();
        }

        private void filePrint_Click(object sender, RoutedEventArgs e)
        {
            Print();
        }

        private void ribbon_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RibbonTab tab = (RibbonTab)ribbon.SelectedItem;
            if (tab.Header.Equals("Debug"))
            {
                if (null != tabDebug) tabControlResults.SelectedItem = tabDebug;
            }
            else
            {
                if (null != tabOutput) tabControlResults.SelectedItem = tabOutput;
            }
        }

        private void debugDataSet(object sender, RoutedEventArgs e)
        {
            try
            {
                Button button = (Button)sender;
                DebugData data = (DebugData)button.Tag;
                SetValue(data.Variable, data.Value);
            }
            catch
            {

            }
        }

        private void debugDataRefresh(object sender, RoutedEventArgs e)
        {
            try
            {
                Button button = (Button)sender;
                DebugData data = (DebugData)button.Tag;
                debugUpdated = false;
            }
            catch
            {
                debugUpdated = false;
            }
        }

        private void viewZoomIn_Click(object sender, RoutedEventArgs e)
        {
            ZoomIn();
        }

        private void viewZoomOut_Click(object sender, RoutedEventArgs e)
        {
            ZoomOut();
        }

        private void viewZoomReset_Click(object sender, RoutedEventArgs e)
        {
            ZoomReset();
        }

        private void helpClick(object sender, RoutedEventArgs e)
        {
            Help();
        }

        public static void Help()
        {
            MessageBox.Show(Properties.Strings.String51 + "\n\n" +
                Properties.Strings.String52 + "\n\n" +
                Properties.Strings.String53 + "\n\n" +
                Properties.Strings.String54 + "\n\n" +
                Properties.Strings.String55 + "\n\n",
                "SB-Prime", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ClickMRU(object sender, MouseButtonEventArgs e)
        {
            ListBoxItem item = (ListBoxItem)sender;
            string MRU = (string)((Grid)item.Content).Tag;
            if (File.Exists(MRU))
            {
                AddDocument();
                activeDocument.LoadDataFromFile(MRU);
                activeTab.Header = new TabHeader(MRU);
                SetTabHeaderStyle(activeTab);
            }
        }

        private void debugDataDelete(object sender, RoutedEventArgs e)
        {
            try
            {
                Button button = (Button)sender;
                DebugData data = (DebugData)button.Tag;
                debugData.Remove(data);
            }
            catch (Exception ex)
            {

            }
        }

        private void graduateVB_Click(object sender, RoutedEventArgs e)
        {
            GraduateVB();
        }

        private void viewTheme_Click(object sender, RoutedEventArgs e)
        {
            Theme();
        }

        private void viewLanguage_TextChanged(object sender, TextChangedEventArgs e)
        {
            SBInterop.Language = viewLanguage.Text;
        }

        private void dataGridResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (null == dataGridResults.SelectedItem) return;
            Error error = (Error)dataGridResults.SelectedItem;
            if (error.Row > 0 && error.Col > 0 && error.Row <= activeDocument.TextArea.Lines.Count)
            {
                activeDocument.SelectLine(error.Row - 1);
            }
        }

        private void fileSearcher_Click(object sender, RoutedEventArgs e)
        {
            if (Dialogs.FileSearcher.Active) return;

            Dialogs.FileSearcher fs = new Dialogs.FileSearcher();
            fs.Show();
        }

        private bool isManualEditCommit = false;
        private void dataGridDebug_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            try
            {
                if (!isManualEditCommit)
                {
                    isManualEditCommit = true;
                    DataGrid grid = (DataGrid)sender;
                    grid.CommitEdit(DataGridEditingUnit.Row, true);
                    isManualEditCommit = false;
                }
            }
            catch (Exception ex)
            {

            }
        }

        private void dataGridDebug_CurrentCellChanged(object sender, EventArgs e)
        {
        }

        private void settingsColor_Click(object sender, RoutedEventArgs e)
        {
            Dialogs.Colours fs = new Dialogs.Colours(this);
            fs.ShowDialog();
            SetWindowColors();
        }

        private void ToolsColor_Click(object sender, RoutedEventArgs e)
        {
            Dialogs.PopupList popup = new Dialogs.PopupList(this, 0);
            popup.Show();
        }

        private void ToolsFont_Click(object sender, RoutedEventArgs e)
        {
            Dialogs.PopupList popup = new Dialogs.PopupList(this, 1);
            popup.Show();
        }

        private void ToolsExtensionSearcher_Click(object sender, RoutedEventArgs e)
        {
            if (Dialogs.ExtensionSearcher.Active) return;

            Dialogs.ExtensionSearcher fs = new Dialogs.ExtensionSearcher();
            fs.Show();
        }

        private void toolsStack_Click(object sender, RoutedEventArgs e)
        {
            if (Dialogs.StackVariables.Active) return;
            stackVariables = new Dialogs.StackVariables(this);
            stackVariables.Show();
        }

        private void settingsExport_Click(object sender, RoutedEventArgs e)
        {
            ExportSettings();
        }

        private void settingsImport_Click(object sender, RoutedEventArgs e)
        {
            ImportSettings();
        }

        private void settingsReset_Click(object sender, RoutedEventArgs e)
        {
            ResetSettings();
        }

        private void fileHightlight_Click(object sender, RoutedEventArgs e)
        {
            HighLightAll();
        }

        private void fileExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        //WPF - Hotkeys that work when TextArea doesn't have focus
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5 && e.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                Run();
                e.Handled = true;
            }
            else if (e.Key == Key.F5 && e.KeyboardDevice.Modifiers == ModifierKeys.Shift)
            {
                Kill();
                e.Handled = true;
            }
            else if (e.Key == Key.F6 && e.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                Debug(true);
                e.Handled = true;
            }
            else if (e.Key == Key.F6 && e.KeyboardDevice.Modifiers == ModifierKeys.Shift)
            {
                Stop();
                e.Handled = true;
            }
            else if (e.Key == Key.F9 && e.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                ToggleBP();
                e.Handled = true;
            }
            else if (e.Key == Key.F10 && e.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                StepOver();
                e.Handled = true;
            }
            else if (e.Key == Key.F11 && e.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                Step();
                e.Handled = true;
            }
            else if (e.Key == Key.N && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                AddDocument();
                e.Handled = true;
            }
            else if (e.Key == Key.O && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                FileOpen();
                e.Handled = true;
            }
            else if (e.Key == Key.S && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                FileSave();
                e.Handled = true;
            }
            else if (e.Key == Key.S && e.KeyboardDevice.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                Publish();
                e.Handled = true;
            }
            else if (e.Key == Key.O && e.KeyboardDevice.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                Import();
                e.Handled = true;
            }
            else if (e.Key == Key.F && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                OpenFindDialog();
                e.Handled = true;
            }
            else if (e.Key == Key.H && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                OpenReplaceDialog();
                e.Handled = true;
            }
            else if (e.Key == Key.F3 && e.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                FindNext();
                e.Handled = true;
            }
            else if (e.Key == Key.F3 && e.KeyboardDevice.Modifiers == ModifierKeys.Shift)
            {
                FindPrevious();
                e.Handled = true;
            }
        }

        //Forms - Hotkeys that work when TextArea has focus
        private void Window_PreviewKeyDown(object sender, System.Windows.Forms.PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == System.Windows.Forms.Keys.F5 && e.Modifiers == System.Windows.Forms.Keys.None)
            {
                Run();
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.F5 && e.Modifiers == System.Windows.Forms.Keys.Shift)
            {
                Kill();
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.F6 && e.Modifiers == System.Windows.Forms.Keys.None)
            {
                Debug(true);
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.F6 && e.Modifiers == System.Windows.Forms.Keys.Shift)
            {
                Stop();
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.F9 && e.Modifiers == System.Windows.Forms.Keys.None)
            {
                ToggleBP();
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.F10 && e.Modifiers == System.Windows.Forms.Keys.None)
            {
                StepOver();
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.F11 && e.Modifiers == System.Windows.Forms.Keys.None)
            {
                Step();
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.F && e.Modifiers == System.Windows.Forms.Keys.Control)
            {
                OpenFindDialog();
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.N && e.Modifiers == System.Windows.Forms.Keys.Control)
            {
                AddDocument();
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.O && e.Modifiers == System.Windows.Forms.Keys.Control)
            {
                MarkedForHotKey.Enqueue(FileOpen);
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.S && e.Modifiers == System.Windows.Forms.Keys.Control)
            {
                MarkedForHotKey.Enqueue(FileSave);
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.S && e.Modifiers == (System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift))
            {
                // Prints an s for some reason
                MarkedForHotKey.Enqueue(Publish);
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.O && e.Modifiers == (System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift))
            {
                MarkedForHotKey.Enqueue(Import);
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.H && e.Modifiers == System.Windows.Forms.Keys.Control)
            {
                OpenReplaceDialog();
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.F3 && e.Modifiers == System.Windows.Forms.Keys.None)
            {
                FindNext();
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.F3 && e.Modifiers == System.Windows.Forms.Keys.Shift)
            {
                FindPrevious();
            }
        }

        private void settingsOptions_Click(object sender, RoutedEventArgs e)
        {
            Options opt = new Options(this);
            opt.Owner = GetWindow(this);
            opt.ShowDialog();
        }

        private void dataGridResults_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            Error error = (Error)e.Row.DataContext;

            if (error.Level > 0)
            {
                e.Row.Background = new SolidColorBrush(IntToColor(FIND_HIGHLIGHT_COLOR)) { Opacity = 0.25 };
            }
            else
            {
                e.Row.Background = new SolidColorBrush(IntToColor(BACKGROUND_COLOR)) { Opacity = 1.0 };
            }
        }

        private void toolsDebugGuide_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://litdev.co.uk/forum/HowToDebug.pdf");
        }

        private void fileWholeWord_Click(object sender, RoutedEventArgs e)
        {
            searchFlags ^= SearchFlags.WholeWord;
            activeDocument.searchManager.HighLight(highlightAll ? activeDocument.searchManager.LastHighLight : "");
        }

        private void fileCaseSensitive_Click(object sender, RoutedEventArgs e)
        {
            searchFlags ^= SearchFlags.MatchCase;
            activeDocument.searchManager.HighLight(highlightAll ? activeDocument.searchManager.LastHighLight : "");
        }

        private void ToolsFlowChart_Click(object sender, RoutedEventArgs e)
        {
            if (FlowChart.Active)
            {
                FlowChart.THIS.Display();
                FlowChart.THIS.Activate();
                if (FlowChart.THIS.WindowState == WindowState.Minimized)
                    FlowChart.THIS.WindowState = WindowState.Normal;
                return;
            }

            FlowChart fc = new FlowChart(MainWindow.THIS);
            fc.Show();
        }

        private void ToolsShapesEditor_Click(object sender, RoutedEventArgs e)
        {
            if (ShapesEditor.Active)
            {
                ShapesEditor.THIS.Display();
                ShapesEditor.THIS.Activate();
                if (ShapesEditor.THIS.WindowState == WindowState.Minimized)
                    ShapesEditor.THIS.WindowState = WindowState.Normal;
                return;
            }

            ShapesEditor ce = new ShapesEditor(MainWindow.THIS);
            ce.Show();
        }

        private void editForwards_Click(object sender, RoutedEventArgs e)
        {
            activeDocument.GoForwards();
        }

        private void editBackwards_Click(object sender, RoutedEventArgs e)
        {
            activeDocument.GoBackwards();
        }

        private void settingsUpdate_Click(object sender, RoutedEventArgs e)
        {
            Update();
        }

        public void Update()
        {
            try
            {
                Process.Start(exeFolder + "\\Update.exe");
                Close();
            }
            catch
            {

            }
        }

        private void ribbon_Loaded(object sender, RoutedEventArgs e)
        {
            Grid child = VisualTreeHelper.GetChild((DependencyObject)sender, 0) as Grid;
            if (child != null)
            {
                child.RowDefinitions[0].Height = new GridLength(0);
            }
            cbFindControl.SelectionBoxWidth += tbFind.ActualWidth - cbFindControl.ActualWidth;
        }

        private void ToolsLinks_Click(object sender, RoutedEventArgs e)
        {
            Links links = new Links();
            links.Owner = GetWindow(this);
            links.ShowDialog();
        }

        private void decompileCS_Click(object sender, RoutedEventArgs e)
        {
            DecompileCS();
        }

        private void viewNumberMargin_Click(object sender, RoutedEventArgs e)
        {
            NumberMargin();
        }

        /*
private bool mRestoreForDragMove;

private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
   if (e.ClickCount == 2)
   {
       if (ResizeMode != ResizeMode.CanResize &&
           ResizeMode != ResizeMode.CanResizeWithGrip)
       {
           return;
       }

       WindowState = WindowState == WindowState.Maximized
           ? WindowState.Normal
           : WindowState.Maximized;
   }
   else
   {
       mRestoreForDragMove = WindowState == WindowState.Maximized;
       DragMove();
   }
}

private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
{
   if (mRestoreForDragMove)
   {
       mRestoreForDragMove = false;

       var point = PointToScreen(e.MouseDevice.GetPosition(this));

       Left = point.X - (RestoreBounds.Width * 0.5);
       Top = point.Y;

       WindowState = WindowState.Normal;
   }
}

private void Window_MouseMove(object sender, MouseEventArgs e)
{
   mRestoreForDragMove = false;
}
*/
    }
}
