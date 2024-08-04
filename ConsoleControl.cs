using System;
using System.Windows.Forms;

namespace NXToolGUI
{
    public class ConsoleControl : TextBox
    {
        public ConsoleControl()
        {
            this.Multiline = true;
            this.ScrollBars = ScrollBars.Vertical;
            this.ReadOnly = true;
            this.ForeColor = System.Drawing.Color.White;
            this.BackColor = System.Drawing.Color.Black;
            this.Font = new System.Drawing.Font("Consolas", 10);
        }

        public void WriteLine(string text)
        {
            this.AppendText(text + Environment.NewLine);
        }
    }
}