﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shell;

namespace SB_Prime
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public string[] Arguments { get; set; }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Arguments = e.Args;
        }
    }
}
