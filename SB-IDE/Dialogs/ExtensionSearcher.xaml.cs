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
        public static double GridWidth;
        public static List<ImageSource> Images = new List<ImageSource>();
        List<TreeViewItem> searchResults = new List<TreeViewItem>();
        int currentSearch = 0;

        public ExtensionSearcher()
        {
            InitializeComponent();

            FontSize = 12 + MainWindow.zoom;
            Topmost = true;

            buttonNext.IsEnabled = false;
            buttonPrevious.IsEnabled = false;

            Left = SystemParameters.PrimaryScreenWidth - Width - 20;
            Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            GridWidth = gridMain.ActualWidth;
            Images.Add(MainWindow.ImageSourceFromBitmap(Properties.Resources.AppIcon));
            Images.Add(MainWindow.ImageSourceFromBitmap(Properties.Resources.IntellisenseObject));
            Images.Add(MainWindow.ImageSourceFromBitmap(Properties.Resources.IntellisenseMethod));
            Images.Add(MainWindow.ImageSourceFromBitmap(Properties.Resources.IntellisenseProperty));
            Images.Add(MainWindow.ImageSourceFromBitmap(Properties.Resources.IntellisenseEvent));

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
                    itemExtension.Header = new Header(0, obj.extension);
                    treeViewSearch.Items.Add(itemExtension);
                }

                itemObject = new TreeViewItem();
                itemObject.Header = new Header(1, obj.name);
                itemExtension.Items.Add(itemObject);

                if (null != obj.summary)
                {
                    itemText = new TreeViewItem();
                    itemText.Header = new Header(-1, obj.summary);
                    itemObject.Items.Add(itemText);
                }

                foreach (Member member in obj.members)
                {
                    itemMember = new TreeViewItem();
                    switch (member.type)
                    {
                        case System.Reflection.MemberTypes.Method:
                            itemMember.Header = new Header(2, member.name);
                            break;
                        case System.Reflection.MemberTypes.Property:
                            itemMember.Header = new Header(3, member.name);
                            break;
                        case System.Reflection.MemberTypes.Event:
                            itemMember.Header = new Header(4, member.name);
                            break;
                    }
                    itemObject.Items.Add(itemMember);

                    if (null != member.summary)
                    {
                        itemText = new TreeViewItem();
                        itemText.Header = new Header(-1, member.summary);
                        itemMember.Items.Add(itemText);
                    }

                    foreach (KeyValuePair<string, string> pair in member.arguments)
                    {
                        itemText = new TreeViewItem();
                        itemText.Header = new Header(-1, "Parameter " + pair.Key + "\n" + pair.Value, true);
                        itemMember.Items.Add(itemText);
                    }

                    if (null != member.returns)
                    {
                        itemText = new TreeViewItem();
                        itemText.Header = new Header(-1, "Returns \n" + member.returns, true);
                        itemMember.Items.Add(itemText);
                    }

                    foreach (KeyValuePair<string, string> pair in member.other)
                    {
                        itemText = new TreeViewItem();
                        itemText.Header = new Header(-1, pair.Key + "\n" + pair.Value, true);
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
            Header header = ((Header)item.Header);
            if (textBoxSearchText.Text == "" || textBoxSearchText.Text == "Search Text")
            {
                item.IsExpanded = false;
                header.HighLight("");
            }
            else
            {
                if (header.Text.ToUpper().Contains(textBoxSearchText.Text.ToUpper()))
                {
                    header.HighLight(textBoxSearchText.Text);
                    TreeViewItem parent = item;
                    searchResults.Add(item);
                    while (null != parent)
                    {
                        parent.IsExpanded = true;

                        if (null != parent.Parent && parent.Parent.GetType() == typeof(TreeViewItem)) parent = (TreeViewItem)parent.Parent;
                        else parent = null;
                    }
                }
                else
                {
                    item.IsExpanded = false;
                    header.HighLight("");
                }
            }
            if (null == item.Items) return;
            foreach (TreeViewItem subItem in item.Items)
            {
                Search(subItem);
            }
        }

        private void buttonSearch_Click(object sender, RoutedEventArgs e)
        {
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;

            searchResults.Clear();
            foreach (TreeViewItem item in treeViewSearch.Items)
            {
                Search(item);
            }
            if (searchResults.Count > 0)
            {
                buttonNext.IsEnabled = true;
                buttonPrevious.IsEnabled = true;
                currentSearch = 0;
                searchResults[currentSearch].IsSelected = true;
                searchResults[currentSearch].BringIntoView();
            }
            else
            {
                buttonNext.IsEnabled = false;
                buttonPrevious.IsEnabled = false;
            }

            Mouse.OverrideCursor = cursor;
        }

        private void buttonCollapse_Click(object sender, RoutedEventArgs e)
        {
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;

            foreach (TreeViewItem item in treeViewSearch.Items)
            {
                Collapse(item);
            }

            Mouse.OverrideCursor = cursor;
        }

        private void buttonExpand_Click(object sender, RoutedEventArgs e)
        {
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;

            if (null == treeViewSearch.SelectedItem) return;
            Expand((TreeViewItem)treeViewSearch.SelectedItem);

            Mouse.OverrideCursor = cursor;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            GridWidth = gridMain.ActualWidth;
        }

        private void buttonNext_Click(object sender, RoutedEventArgs e)
        {
            currentSearch++;
            if (currentSearch >= searchResults.Count) currentSearch = 0;
            searchResults[currentSearch].IsSelected = true;
            searchResults[currentSearch].BringIntoView();
        }

        private void buttonPrevious_Click(object sender, RoutedEventArgs e)
        {
            currentSearch--;
            if (currentSearch < 0) currentSearch = searchResults.Count - 1;
            searchResults[currentSearch].IsSelected = true;
            searchResults[currentSearch].BringIntoView();
        }
    }

    class Header : Grid
    {
        public string Text;

        public Header(int level, string text, bool titled = false)
        {
            Text = text;
            while (Text.Contains("\n ")) Text = Text.Replace("\n ", "\n");
            RowDefinitions.Add(new RowDefinition() { });
            RowDefinitions.Add(new RowDefinition() { });
            ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(25) });
            ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(ExtensionSearcher.GridWidth - 130) });

            if (level >= 0)
            {
                Image img = new Image()
                {
                    Width = 20,
                    Height = 20,
                    Source = ExtensionSearcher.Images[level]
                };
                if (null != img.Source)
                {
                    Children.Add(img);
                    SetRow(img, 0);
                    SetColumn(img, 0);
                }

            }
            if (titled)
            {
                int pos = Text.IndexOf('\n');
                string title = Text.Substring(0, pos);
                string details = Text.Substring(pos + 1);

                TextBlock tb1 = new TextBlock() { Text = title, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap };
                tb1.Tag = title;
                Children.Add(tb1);
                SetRow(tb1, 0);
                SetColumn(tb1, 1);
                TextBlock tb2 = new TextBlock() { Text = details, TextWrapping = TextWrapping.Wrap };
                tb2.Tag = details;
                Children.Add(tb2);
                SetRow(tb2, 1);
                SetColumn(tb2, 1);
            }
            else
            {
                TextBlock tb = new TextBlock() { Text = Text, TextWrapping = TextWrapping.Wrap };
                tb.Tag = Text;
                Children.Add(tb);
                SetRow(tb, 0);
                SetColumn(tb, 1);
            }
        }

        public void HighLight(string highlight)
        {
            foreach (UIElement elt in Children)
            {
                if (elt.GetType() == typeof(TextBlock))
                {
                    TextBlock tb = (TextBlock)elt;
                    tb.Inlines.Clear();
                    if (highlight == "")
                    {
                        tb.Text = (string)tb.Tag;
                    }
                    else
                    {
                        string txt = (string)tb.Tag;
                        string search = highlight;
                        int pos = txt.ToUpper().IndexOf(search.ToUpper());
                        int len = highlight.Length;
                        while (pos >= 0)
                        {
                            tb.Inlines.Add(txt.Substring(0, pos));
                            tb.Inlines.Add(new Run(txt.Substring(pos, len)) { Background = new SolidColorBrush(MainWindow.IntToColor(MainWindow.FIND_HIGHLIGHT_COLOR)) { Opacity = 0.25 }, FontStyle = FontStyles.Italic, FontWeight = FontWeights.Bold });
                            txt = txt.Substring(pos + len);
                            pos = txt.ToUpper().IndexOf(search.ToUpper());
                        }
                        if (txt.Length > 0) tb.Inlines.Add(txt);
                    }
                }
            }
        }
    }
}
