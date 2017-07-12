using System;
using System.Windows.Forms;
using Jil;

namespace WindowsLayoutSnapshot
{
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main()
        {
            JSON.SetDefaultOptions(Options.ISO8601PrettyPrintCamelCase);
            
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayIconForm());
        }
    }
}