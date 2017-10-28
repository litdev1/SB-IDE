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
        SBDocument sbDocument;

        public FindAndReplace(SBDocument sbDocument)
        {
            this.sbDocument = sbDocument;
            InitializeComponent();

            Topmost = true;
            if (sbDocument.TextArea.SelectedText != "") textBoxFind.Text = sbDocument.TextArea.SelectedText;
            textBoxFind.Focus();
            textBoxFind.SelectAll();
        }

        private void buttonFind_Click(object sender, RoutedEventArgs e)
        {
            sbDocument.searchManager.Find(true, textBoxFind.Text);
        }

        private void buttonReplace_Click(object sender, RoutedEventArgs e)
        {
            if (sbDocument.TextArea.SelectedText.ToUpper() != textBoxFind.Text.ToUpper())
            {
                sbDocument.searchManager.Find(true, textBoxFind.Text);
            }
            if (sbDocument.TextArea.SelectedText.ToUpper() != textBoxFind.Text.ToUpper()) return;

            int iStart = sbDocument.TextArea.SelectionStart;
            int iLen = sbDocument.TextArea.SelectedText.Length;
            sbDocument.TextArea.SetTargetRange(iStart, iStart + iLen);
            sbDocument.TextArea.ReplaceTarget(textBoxReplace.Text);

            sbDocument.searchManager.Find(true, textBoxFind.Text);
        }

        private void buttonReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            if (sbDocument.TextArea.SelectedText.ToUpper() != textBoxFind.Text.ToUpper())
            {
                sbDocument.searchManager.Find(true, textBoxFind.Text);
            }
            if (sbDocument.TextArea.SelectedText.ToUpper() != textBoxFind.Text.ToUpper()) return;

            while (sbDocument.TextArea.SelectedText.ToUpper() == textBoxFind.Text.ToUpper())
            {
                int iStart = sbDocument.TextArea.SelectionStart;
                int iLen = sbDocument.TextArea.SelectedText.Length;
                sbDocument.TextArea.SetTargetRange(iStart, iStart + iLen);
                sbDocument.TextArea.ReplaceTarget(textBoxReplace.Text);

                sbDocument.searchManager.Find(true, textBoxFind.Text);
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Topmost = true;
            Activate();
        }
    }
}
