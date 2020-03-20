using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SB_Prime.Dialogs
{
    /// <summary>
    /// Interaction logic for Links.xaml
    /// </summary>
    public partial class Links : Window
    {
        private List<LinkData> links = new List<LinkData>();

        public Links()
        {
            InitializeComponent();

            FontSize = 12 + MainWindow.zoom;

            dataGridLinks.ItemsSource = links;

            Image image = new Image() { Source = MainWindow.ImageSourceFromBitmap(Properties.Resources.Link) };
            links.Add(new LinkData()
            {
                Name = "Visual Studio",
                Description = Properties.Strings.String75 + "\n" + Properties.Strings.String76 + "\n" + Properties.Strings.String83,
                Link = new Image() { Source = MainWindow.ImageSourceFromBitmap(Properties.Resources.Link_VS) },
                URL = "https://visualstudio.microsoft.com/free-developer-offers"
            });
            links.Add(new LinkData()
            {
                Name = "ILSpy",
                Description = Properties.Strings.String77 + "\n" + Properties.Strings.String78 + "\n" + Properties.Strings.String79,
                Link = new Image() { Source = MainWindow.ImageSourceFromBitmap(Properties.Resources.Link_ILSpy) },
                URL = "https://github.com/icsharpcode/ILSpy/releases/tag/v4.0-beta2"
            });
            links.Add(new LinkData()
            {
                Name = "Paint.Net",
                Description = Properties.Strings.String80 + "\n" + Properties.Strings.String81,
                Link = new Image() { Source = MainWindow.ImageSourceFromBitmap(Properties.Resources.Link_Paint_Net) },
                URL = "https://www.getpaint.net/index.html"
            });
            links.Add(new LinkData()
            {
                Name = "Notepad++",
                Description = Properties.Strings.String82 + "\n" + Properties.Strings.String84,
                Link = new Image() { Source = MainWindow.ImageSourceFromBitmap(Properties.Resources.Link_Notepad__) },
                URL = "https://notepad-plus-plus.org"
            });
            links.Add(new LinkData()
            {
                Name = "jZip",
                Description = Properties.Strings.String85 + "\n" + Properties.Strings.String86,
                Link = new Image() { Source = MainWindow.ImageSourceFromBitmap(Properties.Resources.Link_jZip) },
                URL = "http://www.jzip.com"
            });
            links.Add(new LinkData()
            {
                Name = "Greenfish\nIcon Editor",
                Description = Properties.Strings.String87 + "\n" + Properties.Strings.String88,
                Link = new Image() { Source = MainWindow.ImageSourceFromBitmap(Properties.Resources.Link_Greenfish) },
                URL = "https://download.cnet.com/Greenfish-Icon-Editor-Pro/3000-2193_4-10773415.html"
            });
        }

        private void dataGridLinkSet(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            try
            {
                LinkData data = (LinkData)button.Tag;
                if (null != data)
                {
                    Process.Start(data.URL);
                }
            }
            catch
            {

            }
        }
    }

    public class LinkData
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Image Link { get; set; }
        public string URL { get; set; }
        public string Details
        {
            get { return URL; }
        }
    }
}
