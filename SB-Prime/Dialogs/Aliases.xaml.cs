using Humanizer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Policy;
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
using static System.Resources.ResXFileRef;

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
            enableAliases.IsChecked = FileFilter.EnableAliases;

            Validate();
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            Validate();
            FileFilter.Aliases.Clear();
            foreach (AliasesData alias in aliases)
            {
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
                    if (values.Length <= 2) continue;

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
                alias.Valid = false;
                if (null == alias.Default || null == alias.Alias) continue;
                if (alias.Default.Length < 2 || alias.Alias.Length < 2) continue;
                if (!alias.Default.All(Char.IsLetter) || !alias.Alias.All(Char.IsLetter)) continue;
                bool bDefault = false;
                bool bAlias = true;
                foreach (SBObject obj in SBObjects.objects)
                {
                    if (obj.name.ToUpperInvariant() == alias.Default.ToUpperInvariant())
                    {
                        bDefault = true;
                    }
                    if (obj.name.ToUpperInvariant() == alias.Alias.ToUpperInvariant())
                    {
                        bAlias = false;
                    }
                    foreach (Member member in obj.members)
                    {
                        if (member.name.ToUpperInvariant() == alias.Default.ToUpperInvariant())
                        {
                            bDefault = true;
                        }
                        if (member.name.ToUpperInvariant() == alias.Alias.ToUpperInvariant())
                        {
                            bAlias = false;
                        }
                    }
                }
                alias.Valid = bDefault && bAlias;
            }
            dataGridAliases.ItemsSource = null;
            dataGridAliases.ItemsSource = aliases;
        }

        private void buttonValidate_Click(object sender, RoutedEventArgs e)
        {
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
