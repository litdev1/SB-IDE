using System;
using System.Collections.Generic;
using System.Drawing;
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
    /// Interaction logic for Aliases.xaml
    /// </summary>
    public partial class Aliases : Window
    {
        MainWindow mainWindow;
        private List<AliasesData> aliases = new List<AliasesData>();

        public Aliases()
        {
            InitializeComponent();

            Topmost = MainWindow.topmost;
            FontSize = 12 + MainWindow.zoom;

            dataGridAliases.ItemsSource = aliases;
            foreach (KeyValuePair<string, string> kvp in FileFilter.Aliases)
            {
                aliases.Add(new AliasesData() { Default = kvp.Key, Alias = kvp.Value });
            }
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            FileFilter.Aliases.Clear();
            foreach (AliasesData alias in aliases)
            {
                if (alias.Default.Length < 2 || alias.Alias.Length < 2) continue;
                if (!alias.Default.All(Char.IsLetter) || !alias.Alias.All(Char.IsLetter)) continue;
                FileFilter.Aliases[alias.Default] = alias.Alias;
            }
            Close();
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void dataGridAliases_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                string data = (string)Clipboard.GetData(DataFormats.UnicodeText);
                if (null == data) return;
                string[] lines = data.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    string[] values = line.Split(new char[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (values.Length != 2) continue;

                    aliases.Add(new AliasesData() { Default = values[0], Alias = values[1] });
                }
                dataGridAliases.ItemsSource = null;
                dataGridAliases.ItemsSource = aliases;
            }
        }
    }

    public class AliasesData
    {
        public string Default { get; set; }
        public string Alias { get; set; }
    }
}
