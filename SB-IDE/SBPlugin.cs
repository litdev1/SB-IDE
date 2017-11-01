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
    public class SBplugin
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

                string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\plugins";
                if (!Directory.Exists(path)) return;

                string[] dlls = Directory.GetFiles(path, "*.dll");
                foreach (string dll in dlls)
                {
                    Assembly assembly = Assembly.LoadFrom(dll);
                    RibbonGroup group = new RibbonGroup() { Header = Path.GetFileNameWithoutExtension(path) };
                    tab.Items.Add(group);

                    Type[] types = assembly.GetTypes();
                    foreach (Type type in types)
                    {
                        if (type.IsPublic && type.IsDefined(typeof(SBpluginAttribute), false))
                        {
                            if (null != GetValue(type, "GetGroupName")) group.Header = (string)GetValue(type, "GetGroupName");

                            Plugin plugin = new Plugin(mainWindow);
                            plugins.Add(plugin);
                            plugin.type = type;
                            if (null != GetValue(type, "GetName")) plugin.name = (string)GetValue(type, "GetName");
                            if (null != GetValue(type, "GetBitmap")) plugin.bitmap = (Bitmap)GetValue(type, "GetBitmap");
                            if (null != GetValue(type, "LargeButton")) plugin.largeButton = (bool)GetValue(type, "LargeButton");
                            if (null != GetValue(type, "GetToolTip")) plugin.tooltip = (string)GetValue(type, "GetToolTip");
                            MethodInfo[] methods = type.GetMethods(BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Static);
                            foreach (MethodInfo method in methods)
                            {
                                ParameterInfo[] args = method.GetParameters();
                                if (args.Length == 1 && args[0].ParameterType == typeof(string))
                                    plugin.runText = method;
                                if (args.Length == 1 && args[0].ParameterType == typeof(MainWindow))
                                    plugin.runMainWindow = method;
                            }

                            if (plugin.name != "" && (null != plugin.runText || null != plugin.runMainWindow))
                            {
                                if (!bCreated) mainWindow.ribbon.Items.Add(tab);
                                bCreated = true;
                                RibbonButton button = new RibbonButton() { Label = plugin.name };
                                if (plugin.tooltip != "") button.ToolTip = plugin.tooltip;
                                if (plugin.largeButton) button.LargeImageSource = MainWindow.ImageSourceFromBitmap(plugin.bitmap);
                                else button.SmallImageSource = MainWindow.ImageSourceFromBitmap(plugin.bitmap);
                                group.Items.Add(button);
                                button.Click += new RoutedEventHandler(pluginClick);
                                button.Tag = plugin;
                            }
                            else
                            {
                                MainWindow.Errors.Add(new Error("Plugin Add : Failed to add a plugin in group " + group.Header));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Plugin Add : " + ex.Message));
            }
        }

        private object GetValue(Type type, string name)
        {
            try
            {
                MethodInfo method = type.GetMethod(name, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Static);
                return method.Invoke(null, new object[] { });
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Plugin GetValue : " + ex.Message));
                return null;
            }
        }

        private void pluginClick(object sender, RoutedEventArgs e)
        {
            try
            {
                RibbonButton button = (RibbonButton)sender;
                Plugin plugin = (Plugin)button.Tag;
                if (null != plugin.runText) plugin.runText.Invoke(null, new object[] { mainWindow.GetActiveDocument().TextArea.Text });
                if (null != plugin.runMainWindow) plugin.runMainWindow.Invoke(null, new object[] { mainWindow });
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Plugin Run : " + ex.Message));
            }
        }
    }

    public class Plugin
    {
        MainWindow mainindow;

        public string name;
        public Bitmap bitmap;
        public bool largeButton;
        public string tooltip;
        public MethodInfo runText;
        public MethodInfo runMainWindow;
        public Type type;

        public Plugin(MainWindow mainindow)
        {
            this.mainindow = mainindow;
            name = "";
            bitmap = Properties.Resources.Plugin;
            largeButton = true;
            tooltip = "";
            runText = null;
            runMainWindow = null;
            type = null;
        }
    }
}
