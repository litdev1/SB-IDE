using ScintillaNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
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

namespace SB_IDE
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : RibbonWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();

            InitWindow();
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

        private void fileOpen_Click(object sender, RoutedEventArgs e)
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
            }
        }

        private void fileClose_Click(object sender, RoutedEventArgs e)
        {
            DeleteDocument();
        }

        private void fileSave_Click(object sender, RoutedEventArgs e)
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

        private void viewCollapse_Click(object sender, RoutedEventArgs e)
        {
            Collapse();
        }

        private void viewExpand_Click(object sender, RoutedEventArgs e)
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
            MessageBox.Show("There is currently no extended help.\n\n" +
                "SB-IDE should work with any installed version of Small Basic.\n\n" +
                "To enable this with debugging an extension is directly compiled and installed in the lib folder the first time SB-IDE is started, requiring User Account Control (UAC) permission.\n\n" +
                "Additionally UAC is required the first time a debug session is performed to allow required communication between applications.\n\n" +
                "Debugging requires running to break points or 'stepping' through the code.  Once paused, values can be be viewed by hovering the mouse over a variable, or by adding variable names to the watch list in the debug tab.  Watch list variables may be viewed as they change and may also be modified.  Array values with [] syntax may also be viewed and modified.\n\n",
                "SB_IDE", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CLickMRU(object sender, MouseButtonEventArgs e)
        {
            ListBoxItem item = (ListBoxItem)sender;
            string MRU = (string)((Grid)item.Content).Tag;
            if (File.Exists(MRU))
            {
                AddDocument();
                activeDocument.LoadDataFromFile(MRU);
                activeTab.Header = new TabHeader(MRU);
            }
        }

        private void debugDataDelete(object sender, RoutedEventArgs e)
        {
            try
            {
                Button button = (Button)sender;
                DebugData data = (DebugData)button.Tag;
                debugData.Remove(data);
                dataGridDebug.Items.Refresh();
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

        private void tbFind_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                FindNext();
            }
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
            Dialogs.FileSearcher fs = new Dialogs.FileSearcher();
            fs.Show();
        }
    }
}
