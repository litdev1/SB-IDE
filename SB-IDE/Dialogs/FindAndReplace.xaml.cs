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

namespace SB_IDE.Dialogs
{
    /// <summary>
    /// Interaction logic for FindAndReplace.xaml
    /// </summary>
    public partial class FindAndReplace : Window
    {
        MainWindow mainWindow;
        public static bool Active = false;
        public static string lastFind = "";
        public static string lastReplace = "";

        internal FindAndReplace(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            InitializeComponent();

            FontSize = 12 + MainWindow.zoom;

            Topmost = true;
            SBDocument sbDocument = mainWindow.GetActiveDocument();
            textBoxFind.Text = lastFind;
            textBoxReplace.Text = lastReplace;
            if (sbDocument.TextArea.SelectedText != "") textBoxFind.Text = sbDocument.TextArea.SelectedText;
            textBoxFind.Focus();
            textBoxFind.SelectAll();

            Left = SystemParameters.PrimaryScreenWidth - Width - 20;
            Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;
        }

        private void buttonFind_Click(object sender, RoutedEventArgs e)
        {
            SBDocument sbDocument = mainWindow.GetActiveDocument();
            sbDocument.searchManager.Find(true, textBoxFind.Text);
        }

        private void buttonReplace_Click(object sender, RoutedEventArgs e)
        {
            SBDocument sbDocument = mainWindow.GetActiveDocument();
            if (sbDocument.TextArea.SelectedText.ToUpper() != textBoxFind.Text.ToUpper())
            {
                sbDocument.searchManager.Find(true, textBoxFind.Text);
            }
            if (sbDocument.TextArea.SelectedText.ToUpper() != textBoxFind.Text.ToUpper()) return;

            int iStart = sbDocument.TextArea.SelectionStart;
            int iLen = sbDocument.TextArea.SelectedText.Length;
            sbDocument.TextArea.SetTargetRange(iStart, iStart + iLen);
            sbDocument.TextArea.ReplaceTarget(textBoxReplace.Text);
            sbDocument.TextArea.CurrentPosition = sbDocument.TextArea.SelectionStart + textBoxReplace.Text.Length;

            sbDocument.searchManager.Find(true, textBoxFind.Text);
        }

        private void buttonReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            SBDocument sbDocument = mainWindow.GetActiveDocument();
            if (sbDocument.TextArea.SelectedText.ToUpper() != textBoxFind.Text.ToUpper())
            {
                sbDocument.searchManager.Find(true, textBoxFind.Text);
            }
            if (sbDocument.TextArea.SelectedText.ToUpper() != textBoxFind.Text.ToUpper()) return;

            List<int> markStart = new List<int>();
            while (sbDocument.TextArea.SelectedText.ToUpper() == textBoxFind.Text.ToUpper())
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
            lastFind = textBoxFind.Text;
            lastReplace = textBoxReplace.Text;
            Active = false;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Active = true;
        }
    }
}
