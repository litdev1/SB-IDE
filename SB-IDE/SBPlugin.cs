using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Ribbon;

namespace SB_IDE
{
    public class SBpluginAttribute : Attribute
    {
    }

    class SBplugin
    {
        List<Plugin> plugins = new List<Plugin>();
        MainWindow mainWindow;

        public SBplugin(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;

            try
            {
                bool bCreated = false;
                RibbonTab tab = new RibbonTab() { Header = "Plugins", KeyTip = "P" };
                RibbonGroup group = new RibbonGroup() { Header = "External Features" };
                tab.Items.Add(group);

                string path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\plugins";
                if (!Directory.Exists(path)) return;
                string[] dlls = Directory.GetFiles(path, "*.dll");
                foreach (string dll in dlls)
                {
                    Assembly assembly = Assembly.LoadFrom(dll);
                    Type[] types = assembly.GetTypes();
                    foreach (Type type in types)
                    {
                        if (type.IsPublic && type.IsDefined(typeof(SBpluginAttribute), false))
                        {
                            Plugin plugin = new Plugin();
                            plugins.Add(plugin);
                            plugin.name = (string)type.GetMethod("GetName", BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { });
                            plugin.bitmap = (Bitmap)type.GetMethod("GetBitmap", BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { });
                            plugin.tooltip = (string)type.GetMethod("GetToolTip", BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { });
                            plugin.run = type.GetMethod("Run", BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Static);

                            if (!bCreated)
                            {
                                mainWindow.ribbon.Items.Add(tab);
                                bCreated = true;
                            }
                            RibbonButton button = new RibbonButton() { Label = plugin.name, LargeImageSource = MainWindow.ImageSourceFromBitmap(plugin.bitmap), ToolTipTitle = plugin.tooltip };
                            group.Items.Add(button);
                            button.Click += new RoutedEventHandler(pluginClick);
                            button.Tag = plugin;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Plugin Add : " + ex.Message));
            }
        }

        private void pluginClick(object sender, RoutedEventArgs e)
        {
            RibbonButton button = (RibbonButton)sender;
            Plugin plugin = (Plugin)button.Tag;
            plugin.Run(mainWindow.GetActiveDocument().TextArea.Text);
        }
    }

    class Plugin
    {
        public string name { get; set; }
        public Bitmap bitmap { get; set; }
        public string tooltip { get; set; }
        public MethodInfo run { get; set; }

        public bool Run(string text)
        {
            try
            {
                return (bool)run.Invoke(null, new object[] { text });
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Plugin Run : " + ex.Message));
                return false;
            }
        }
    }
}
