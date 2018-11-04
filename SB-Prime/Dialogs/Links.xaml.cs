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
                Description = "Microsoft Visual Studio Community development environment.\nWrite your own extensions and bigger projects.\nA big download, but a professional level IDE.",
                Link = new Image() { Source = MainWindow.ImageSourceFromBitmap(Properties.Resources.Link_VS) },
                URL = "https://visualstudio.microsoft.com/free-developer-offers"
            });
            links.Add(new LinkData()
            {
                Name = "ILSpy",
                Description = "View and decompile .Net assemblies.  Find out how extensions work.\nEssential for getting under the hood of Small Basic.\nDownload zip binaries and extract (no installation).",
                Link = new Image() { Source = MainWindow.ImageSourceFromBitmap(Properties.Resources.Link_ILSpy) },
                URL = "https://github.com/icsharpcode/ILSpy/releases/tag/v4.0-beta2"
            });
            links.Add(new LinkData()
            {
                Name = "Paint.Net",
                Description = "Create and manipulate images, including easily giving png images transparent backgrounds and creating animated gifs.\nCheck out the magic wand tool.",
                Link = new Image() { Source = MainWindow.ImageSourceFromBitmap(Properties.Resources.Link_Paint_Net) },
                URL = "https://www.getpaint.net/index.html"
            });
            links.Add(new LinkData()
            {
                Name = "Notepad++",
                Description = "Excellent text editor alternative to notepad.\nUseful to quickly look at or edit most text readable file formats.",
                Link = new Image() { Source = MainWindow.ImageSourceFromBitmap(Properties.Resources.Link_Notepad__) },
                URL = "https://notepad-plus-plus.org"
            });
            links.Add(new LinkData()
            {
                Name = "jZip",
                Description = "File zip compression.\nThe best way to share multiple documents in a single compressed file.",
                Link = new Image() { Source = MainWindow.ImageSourceFromBitmap(Properties.Resources.Link_jZip) },
                URL = "http://www.jzip.com"
            });
            links.Add(new LinkData()
            {
                Name = "Greenfish\nIcon Editor",
                Description = "Create and edit icons.\nIcon formats are quite tricky without a simple tool like this.",
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
