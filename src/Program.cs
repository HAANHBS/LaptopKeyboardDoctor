using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace LaptopKeyboardDoctor
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
            {
                string output = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "LaptopKeyboardDoctor-self-test-result.txt");
                try
                {
                    string result = DetectorSelfTests.RunAll();
                    File.WriteAllText(output, result, new UTF8Encoding(false));
                    return 0;
                }
                catch (Exception ex)
                {
                    File.WriteAllText(output, "SELF TEST: FAIL\r\n" + ex, new UTF8Encoding(false));
                    return 1;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                MessageBox.Show(e.Exception.ToString(), "Lỗi ứng dụng", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            Application.Run(new MainForm());
            return 0;
        }
    }
}
