using System;
using System.Windows.Forms;

namespace AndonTerminal
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Sửa dòng này để nó chạy cái TerminalMainForm
            Application.Run(new Forms.TerminalMainForm());
        }
    }
}