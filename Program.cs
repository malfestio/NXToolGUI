using System;
using System.Windows.Forms;

namespace NXToolGUI
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new NXToolGUI()); // Initialize NXToolGUI
        }
    }
}