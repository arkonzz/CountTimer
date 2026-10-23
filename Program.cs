namespace CountTimer
{
    internal static class Program
    {
        private static Mutex mutex = new Mutex(true, "{8F6F0AC4-B9A1-45fd-A8CF-72F04E6BDE8F}");
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                // To customize application configuration such as set high DPI settings or default font,
                // see https://aka.ms/applicationconfiguration.
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ApplicationConfiguration.Initialize();
                AntdUI.Config.ShowInWindow = true;
                // 全局主色定为番茄红，与番茄钟专注色一致
                AntdUI.Style.SetPrimary(System.Drawing.Color.FromArgb(228, 88, 53));
                Application.Run(new MainForm());
                mutex.ReleaseMutex();
            }
            else
            {
                MessageBox.Show("程序已在运行中", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
