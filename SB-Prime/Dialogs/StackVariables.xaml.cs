using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// Interaction logic for StackVariables.xaml
    /// </summary>
    public partial class StackVariables : Window
    {
        public static bool Active = false;
        private MainWindow mainWindow;

        public ObservableCollection<VariableData> variables = new ObservableCollection<VariableData>();
        public ObservableCollection<StackData> stacks = new ObservableCollection<StackData>();

        public StackVariables(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;

            InitializeComponent();

            Topmost = true;
            FontSize = 12 + MainWindow.zoom;

            dataGridVariables.ItemsSource = variables;
            dataGridStack.ItemsSource = stacks;

            Left = SystemParameters.PrimaryScreenWidth - Width - 20;
            Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Topmost = true;
            Activate();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Active = true;
            MainWindow.GetStackVariables = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Active = false;
            MainWindow.GetStackVariables = false;
        }

        public void Update()
        {
            variables.Clear();
            foreach (KeyValuePair<string, string> kvp in mainWindow.GetActiveDocument().debug.Variables)
            {
                variables.Add(new VariableData() { Variable = kvp.Key, Value = kvp.Value });
            }

            stacks.Clear();
            foreach (string stack in mainWindow.GetActiveDocument().debug.CallStack)
            {
                stacks.Add(new StackData() { Stack = stack });
            }
        }
    }

    public class VariableData
    {
        public string Variable { get; set; }
        public string Value { get; set; }
    }

    public class StackData
    {
        public string Stack { get; set; }
    }
}
