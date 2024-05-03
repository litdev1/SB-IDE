using Humanizer;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
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
        private List<AliasesData> aliases = new List<AliasesData>();
        private static string word = "^[" + MainWindow.exRegex + "A-Za-z_][" + MainWindow.exRegex + "A-Za-z_0-9]*$";

        public Aliases()
        {
            InitializeComponent();

            Topmost = MainWindow.topmost;
            FontSize = 12 + MainWindow.zoom;

            dataGridAliases.ItemsSource = aliases;
            foreach (KeyValuePair<string, string> kvp in FileFilter.AllAliases)
            {
                aliases.Add(new AliasesData() { Default = kvp.Key, Alias = kvp.Value });
            }
            enableAliases.IsChecked = FileFilter.EnableAliases;

            Validate();
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            Validate();
            FileFilter.Aliases.Clear();
            FileFilter.AllAliases.Clear();
            foreach (AliasesData alias in aliases)
            {
                FileFilter.AllAliases[alias.Default] = alias.Alias;
                if (!alias.Valid) continue;
                FileFilter.Aliases[alias.Default] = alias.Alias;
            }
            FileFilter.EnableAliases = (bool)enableAliases.IsChecked;
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
                    if (values.Length < 2) continue;
                    aliases.Add(new AliasesData() { Default = values[0], Alias = values[1] });
                }
                Validate();
            }
        }

        private void buttonHelp_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(Properties.Strings.Label500 + "\n\n" +
                Properties.Strings.Label501 + "\n\n" +
                Properties.Strings.Label502,
                "SB-Prime", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Validate()
        {
            foreach (AliasesData alias in aliases)
            {
                alias.Valid = IsValid(alias.Default, alias.Alias);
            }
            dataGridAliases.ItemsSource = null;
            dataGridAliases.ItemsSource = aliases;
        }

        public static bool IsValid(string _default, string _alias)
        {
            if (null == _default || null == _alias) return false;
            if (!Regex.IsMatch(_default, word)) return false;
            if (!Regex.IsMatch(_alias, word)) return false;
            bool bDefault = false;
            bool bAlias = true;
            foreach (SBObject obj in SBObjects.objects)
            {
                if (obj.name.ToUpperInvariant() == _default.ToUpperInvariant())
                {
                    bDefault = true;
                }
                if (obj.name.ToUpperInvariant() == _alias.ToUpperInvariant())
                {
                    bAlias = false;
                }
                foreach (Member member in obj.members)
                {
                    if (member.name.ToUpperInvariant() == _default.ToUpperInvariant())
                    {
                        bDefault = true;
                    }
                    if (member.name.ToUpperInvariant() == _alias.ToUpperInvariant())
                    {
                        bAlias = false;
                    }
                }
            }
            return bDefault && bAlias;
        }

        private void buttonValidate_Click(object sender, RoutedEventArgs e)
        {
            Validate();
        }

        private void buttonExport_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            saveFileDialog.FileName = "Aliases";
            saveFileDialog.Filter = "Alias files (*.txt)|*.txt|All files (*.*)|*.*";
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.RestoreDirectory = true;
            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                List<string> data = new List<string>();
                foreach (AliasesData alias in aliases)
                {
                    data.Add(alias.Default + '\t' + alias.Alias);
                }
                File.WriteAllLines(saveFileDialog.FileName, data.ToArray());
            }
        }

        private void buttonImport_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Alias files (*.txt)|*.txt|All files (*.*)|*.*";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string[] data = File.ReadAllLines(openFileDialog.FileName);
                aliases.Clear();
                foreach (string line in data)
                {
                    string[] values = line.Split(new char[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (values.Length < 2) continue;
                    aliases.Add(new AliasesData() { Default = values[0], Alias = values[1] });
                }
            }
            Validate();
        }

        private void buttonClear_Click(object sender, RoutedEventArgs e)
        {
            aliases.Clear();
            Validate();
        }
    }

    public class AliasesData
    {
        public string Default { get; set; }
        public string Alias { get; set; }
        public bool Valid { get; set; }
    }
}
