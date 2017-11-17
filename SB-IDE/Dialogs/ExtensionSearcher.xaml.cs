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
    /// Interaction logic for ExtensionSearcher.xaml
    /// </summary>
    public partial class ExtensionSearcher : Window
    {
        TreeViewItem itemExtension;
        TreeViewItem itemObject;
        TreeViewItem itemMember;
        TreeViewItem itemText;

        public ExtensionSearcher()
        {
            InitializeComponent();

            FontSize = 12 + MainWindow.zoom;

            Topmost = true;

            Load();
        }

        private void Load()
        {
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;

            treeViewSearch.Items.Clear();
            itemExtension = null;

            foreach (SBObject obj in SBObjects.objects)
            {
                if (null == itemExtension || ((Header)itemExtension.Header).Text != obj.extension)
                {
                    itemExtension = new TreeViewItem();
                    itemExtension.Header = new Header(itemExtension, 0, obj.extension);
                    treeViewSearch.Items.Add(itemExtension);
                }

                itemObject = new TreeViewItem();
                itemObject.Header = new Header(itemObject, 1, obj.name);
                itemExtension.Items.Add(itemObject);

                if (null != obj.summary)
                {
                    itemText = new TreeViewItem();
                    itemText.Header = new Header(itemText, - 1, obj.summary);
                    itemObject.Items.Add(itemText);
                }

                foreach (Member member in obj.members)
                {
                    itemMember = new TreeViewItem();
                    switch (member.type)
                    {
                        case System.Reflection.MemberTypes.Method:
                            itemMember.Header = new Header(itemMember, 2, member.name);
                            break;
                        case System.Reflection.MemberTypes.Property:
                            itemMember.Header = new Header(itemMember, 3, member.name);
                            break;
                        case System.Reflection.MemberTypes.Event:
                            itemMember.Header = new Header(itemMember, 4, member.name);
                            break;
                    }
                    itemObject.Items.Add(itemMember);

                    if (null != member.summary)
                    {
                        itemText = new TreeViewItem();
                        itemText.Header = new Header(itemText, -1, member.summary);
                        itemMember.Items.Add(itemText);
                    }

                    foreach (KeyValuePair<string, string> pair in member.arguments)
                    {
                        itemText = new TreeViewItem();
                        itemText.Header = new Header(itemText, -1, "Parameter " + pair.Key + "\n" + pair.Value, true);
                        itemMember.Items.Add(itemText);
                    }

                    if (null != member.returns)
                    {
                        itemText = new TreeViewItem();
                        itemText.Header = new Header(itemText, -1, "Returns \n" + member.returns, true);
                        itemMember.Items.Add(itemText);
                    }

                    foreach (KeyValuePair<string, string> pair in member.other)
                    {
                        itemText = new TreeViewItem();
                        itemText.Header = new Header(itemText, -1, pair.Key + "\n" + pair.Value, true);
                        itemMember.Items.Add(itemText);
                    }
                }
            }

            Mouse.OverrideCursor = cursor;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Topmost = true;
            Activate();
        }

        private void Collapse(TreeViewItem item)
        {
            item.IsExpanded = false;
            if (null == item.Items) return;
            foreach (TreeViewItem subItem in item.Items)
            {
                Collapse(subItem);
            }
        }

        private void Expand(TreeViewItem item)
        {
            item.IsExpanded = true;
            if (null == item.Items) return;
            foreach (TreeViewItem subItem in item.Items)
            {
                Expand(subItem);
            }
        }

        private void Search(TreeViewItem item)
        {
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;

            if (textBoxSearchText.Text == "" || textBoxSearchText.Text == "Search Text")
            {
                item.IsExpanded = false;
            }
            else
            {
                if (((Header)item.Header).Text.ToUpper().Contains(textBoxSearchText.Text.ToUpper()))
                {
                    TreeViewItem parent = item;
                    while (null != parent)
                    {
                        parent.IsExpanded = true;
                        Header header = (Header)parent.Header;

                        if (null != parent.Parent && parent.Parent.GetType() == typeof(TreeViewItem)) parent = (TreeViewItem)parent.Parent;
                        else parent = null;
                    }
                }
            }
            if (null == item.Items) return;
            foreach (TreeViewItem subItem in item.Items)
            {
                Search(subItem);
            }

            Mouse.OverrideCursor = cursor;
        }

        private void buttonSearch_Click(object sender, RoutedEventArgs e)
        {
            foreach (TreeViewItem item in treeViewSearch.Items)
            {
                Search(item);
            }
        }

        private void buttonCollapse_Click(object sender, RoutedEventArgs e)
        {
            foreach (TreeViewItem item in treeViewSearch.Items)
            {
                Collapse(item);
            }
        }

        private void buttonExpand_Click(object sender, RoutedEventArgs e)
        {
            if (null == treeViewSearch.SelectedItem) return;
            Expand((TreeViewItem)treeViewSearch.SelectedItem);
        }
    }

    class Header : Grid
    {
        public string Text;

        public Header(TreeViewItem item, int level, string text, bool titled = false)
        {
            Text = text;
            while (Text.Contains("\n ")) Text = Text.Replace("\n ", "\n");
            RowDefinitions.Add(new RowDefinition() { });
            RowDefinitions.Add(new RowDefinition() { });
            ColumnDefinitions.Add(new ColumnDefinition() { });
            ColumnDefinitions.Add(new ColumnDefinition() { });

            ImageSource imgSource = null;
            switch (level)
            {
                case 0:
                    imgSource = MainWindow.ImageSourceFromBitmap(Properties.Resources.AppIcon);
                    break;
                case 1:
                    imgSource = MainWindow.ImageSourceFromBitmap(Properties.Resources.IntellisenseObject);
                    break;
                case 2:
                    imgSource = MainWindow.ImageSourceFromBitmap(Properties.Resources.IntellisenseMethod);
                    break;
                case 3:
                    imgSource = MainWindow.ImageSourceFromBitmap(Properties.Resources.IntellisenseProperty);
                    break;
                case 4:
                    imgSource = MainWindow.ImageSourceFromBitmap(Properties.Resources.IntellisenseEvent);
                    break;
                default:
                    break;
            }
            Image img = new Image()
            {
                Width = 20,
                Height = 20,
                Source = imgSource
            };
            if (null != img.Source)
            {
                Children.Add(img);
                SetRow(img, 0);
                SetColumn(img, 0);
            }
            if (titled)
            {
                int pos = Text.IndexOf('\n');
                string title = Text.Substring(0, pos);
                string details = Text.Substring(pos + 1);

                TextBlock tb1 = new TextBlock() { Text = title, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap };
                Children.Add(tb1);
                SetRow(tb1, 0);
                SetColumn(tb1, 1);
                TextBlock tb2 = new TextBlock() { Text = details, TextWrapping = TextWrapping.Wrap };
                Children.Add(tb2);
                SetRow(tb2, 1);
                SetColumn(tb2, 1);
            }
            else
            {
                TextBlock tb = new TextBlock() { Text = Text, TextWrapping = TextWrapping.Wrap };
                Children.Add(tb);
                SetRow(tb, 0);
                SetColumn(tb, 1);
            }
        }
    }
}
