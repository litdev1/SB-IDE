using System;
using System.Collections.Generic;
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
    /// Interaction logic for FindAndReplace.xaml
    /// </summary>
    public partial class FindAndReplace : Window
    {
        MainWindow mainWindow;
        public static bool Active = false;
        public static List<string> lastFind = new List<string>();
        public static List<string> lastReplace = new List<string>();

        internal FindAndReplace(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            InitializeComponent();

            FontSize = 12 + MainWindow.zoom;

            Topmost = true;

            SBDocument sbDocument = mainWindow.GetActiveDocument();
            foreach (string item in mainWindow.cbFindText.Items)
            {
                if (!lastFind.Contains(item)) lastFind.Add(item);
            }
            if (sbDocument.TextArea.SelectedText != "" && !lastFind.Contains(sbDocument.TextArea.SelectedText)) lastFind.Insert(0, sbDocument.TextArea.SelectedText);

            foreach (string item in lastFind)
            {
                comboBoxFind.Items.Add(item);
            }
            foreach (string item in lastReplace)
            {
                comboBoxReplace.Items.Add(item);
            }

            textBoxFind.Text = lastFind.Count > 0 ? lastFind[0] : "";
            textBoxReplace.Text = lastReplace.Count > 0 ? lastReplace[0] : "";
            comboBoxFind.SelectedItem = textBoxFind.Text;
            comboBoxReplace.SelectedItem = textBoxReplace.Text;

            Left = SystemParameters.PrimaryScreenWidth - Width - 20;
            Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;
        }

        private void buttonFindPrevious_Click(object sender, RoutedEventArgs e)
        {
            if (!lastFind.Contains(textBoxFind.Text)) lastFind.Insert(0, textBoxFind.Text);
            if (!comboBoxFind.Items.Contains(textBoxFind.Text)) comboBoxFind.Items.Insert(0, textBoxFind.Text);
            comboBoxFind.SelectedItem = textBoxFind.Text;

            SBDocument sbDocument = mainWindow.GetActiveDocument();
            sbDocument.searchManager.Find(false, textBoxFind.Text);
        }

        private void buttonFindNext_Click(object sender, RoutedEventArgs e)
        {
            if (!lastFind.Contains(textBoxFind.Text)) lastFind.Insert(0, textBoxFind.Text);
            if (!comboBoxFind.Items.Contains(textBoxFind.Text)) comboBoxFind.Items.Insert(0, textBoxFind.Text);
            comboBoxFind.SelectedItem = textBoxFind.Text;

            SBDocument sbDocument = mainWindow.GetActiveDocument();
            sbDocument.searchManager.Find(true, textBoxFind.Text);
        }

        private void buttonReplace_Click(object sender, RoutedEventArgs e)
        {
            if (!lastFind.Contains(textBoxFind.Text)) lastFind.Insert(0, textBoxFind.Text);
            if (!comboBoxFind.Items.Contains(textBoxFind.Text)) comboBoxFind.Items.Insert(0, textBoxFind.Text);
            comboBoxFind.SelectedItem = textBoxFind.Text;
            if (!lastReplace.Contains(textBoxReplace.Text)) lastReplace.Insert(0, textBoxReplace.Text);
            if (!comboBoxReplace.Items.Contains(textBoxReplace.Text)) comboBoxReplace.Items.Insert(0, textBoxReplace.Text);
            comboBoxReplace.SelectedItem = textBoxReplace.Text;

            SBDocument sbDocument = mainWindow.GetActiveDocument();
            if (sbDocument.TextArea.SelectedText.ToUpperInvariant() != textBoxFind.Text.ToUpperInvariant())
            {
                sbDocument.searchManager.Find(true, textBoxFind.Text);
            }
            if (sbDocument.TextArea.SelectedText.ToUpperInvariant() != textBoxFind.Text.ToUpperInvariant()) return;

            int iStart = sbDocument.TextArea.SelectionStart;
            int iLen = sbDocument.TextArea.SelectedText.Length;
            sbDocument.TextArea.SetTargetRange(iStart, iStart + iLen);
            sbDocument.TextArea.ReplaceTarget(textBoxReplace.Text);
            sbDocument.TextArea.CurrentPosition = sbDocument.TextArea.SelectionStart + textBoxReplace.Text.Length;

            sbDocument.searchManager.Find(true, textBoxFind.Text);
        }

        private void buttonReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            if (!lastFind.Contains(textBoxFind.Text)) lastFind.Insert(0, textBoxFind.Text);
            if (!comboBoxFind.Items.Contains(textBoxFind.Text)) comboBoxFind.Items.Insert(0, textBoxFind.Text);
            comboBoxFind.SelectedItem = textBoxFind.Text;
            if (!lastReplace.Contains(textBoxReplace.Text)) lastReplace.Insert(0, textBoxReplace.Text);
            if (!comboBoxReplace.Items.Contains(textBoxReplace.Text)) comboBoxReplace.Items.Insert(0, textBoxReplace.Text);
            comboBoxReplace.SelectedItem = textBoxReplace.Text;

            SBDocument sbDocument = mainWindow.GetActiveDocument();
            if (sbDocument.TextArea.SelectedText.ToUpperInvariant() != textBoxFind.Text.ToUpperInvariant())
            {
                sbDocument.searchManager.Find(true, textBoxFind.Text);
            }
            if (sbDocument.TextArea.SelectedText.ToUpperInvariant() != textBoxFind.Text.ToUpperInvariant()) return;

            List<int> markStart = new List<int>();
            while (sbDocument.TextArea.SelectedText.ToUpperInvariant() == textBoxFind.Text.ToUpperInvariant())
            {
                if (markStart.Contains(sbDocument.TextArea.SelectionStart)) break;
                markStart.Add(sbDocument.TextArea.SelectionStart);
                sbDocument.searchManager.Find(true, textBoxFind.Text);
            }

            int iLen = sbDocument.TextArea.SelectedText.Length;
            markStart.Sort();
            markStart.Reverse();
            foreach (int iStart in markStart)
            {
                sbDocument.TextArea.SetTargetRange(iStart, iStart + iLen);
                sbDocument.TextArea.ReplaceTarget(textBoxReplace.Text);
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Topmost = true;
            Activate();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!lastFind.Contains(textBoxFind.Text)) lastFind.Insert(0, textBoxFind.Text);
            if (!lastReplace.Contains(textBoxReplace.Text)) lastReplace.Insert(0, textBoxReplace.Text);
            foreach (string item in lastFind)
            {
                if (!mainWindow.cbFindText.Items.Contains(item)) mainWindow.cbFindText.Items.Add(item);
            }
            Active = false;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Active = true;
            textBoxReplace.Focus();
            textBoxReplace.SelectAll();
        }

        private void comboBoxFind_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            textBoxFind.Text = comboBoxFind.SelectedItem.ToString();
            textBoxFind.Focus();
            textBoxFind.SelectAll();
        }

        private void comboBoxReplace_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            textBoxReplace.Text = comboBoxReplace.SelectedItem.ToString();
            textBoxReplace.Focus();
            textBoxReplace.SelectAll();
        }
    }
}
