using AvalonDock;
using AvalonDock.Layout;
using AvalonDock.Themes;
using ICSharpCode.Decompiler.TypeSystem;
using ScintillaNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace SB_Prime
{
    /// <summary>
    /// Interaction logic for Docker.xaml
    /// </summary>
    public partial class Docker : UserControl
    {
        private LayoutDocumentPane activePane = null;
        private SBLayout activeLayout = null;

        public Docker()
        {
            InitializeComponent();

            dockManager.Theme = new Vs2013LightTheme();

            activePane = new LayoutDocumentPane();
            DocumentPaneGroup.Children.Add(activePane);
        }

        public SBLayout AddDocument(SBDocument doc)
        {
            WindowsFormsHost host = new WindowsFormsHost();
            host.Child = doc.TextArea;
            activeLayout = new SBLayout();
            activeLayout.Doc = doc;
            activeLayout.Content = host;
            activeLayout.IconSource = MainWindow.ImageSourceFromBitmap(Properties.Resources.AppIcon);
            activePane.Children.Add(activeLayout);

            return activeLayout; //These are equivalent to original tabs
        }

        public LayoutDocumentPane ActivePane
        {
            get { return activePane; }
        }

        public SBLayout ActiveLayout
        {
            get { return activeLayout; }
        }

        public LayoutDocumentPaneGroup Panes
        {
            get { return DocumentPaneGroup; }
        }

        private void OnLayoutRootPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var activeContent = ((LayoutRoot)sender).ActiveContent;
            if (activeContent != null && activeContent.GetType() == typeof(SBLayout))
            {
                activeLayout = (SBLayout)activeContent;
                activePane = (LayoutDocumentPane)activeLayout.Parent;
            }
        }
    }

    public class SBLayout : LayoutDocument
    {
        private SBDocument doc;
        private string filePath;
        private string fileName;

        public SBLayout()
        {
        }

        public void SetPath(string _filePath)
        {
            filePath = _filePath;
            fileName = Path.GetFileName(filePath);
            Title = fileName;
            ToolTip = fileName;
        }

        public SBDocument Doc { get { return doc; } set { doc = value; } }
        public string FilePath { get { return filePath; } }
        public string FileName { get { return fileName; } }
    }
}
