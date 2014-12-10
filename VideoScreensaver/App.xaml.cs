using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Repository;

namespace VideoScreensaver
{

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Logger
        private static readonly ILog logger =
           LogManager.GetLogger(typeof(App));

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT {
            public int left, top, right, bottom;
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        public App()
            : base()
        {
            this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;
        }

        void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            logger.Error("Unhandled Exception", e.Exception);
            string errorMessage = string.Format("An unhandled exception occurred: {0}", e.Exception.Message);
            MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
            FlushLogs();
        }

        // Flush Log4Net logs
        void FlushLogs()
        {
            ILoggerRepository rep = LogManager.GetRepository();
            foreach (IAppender appender in rep.GetAppenders())
            {
                var buffered = appender as BufferingAppenderSkeleton;
                if (buffered != null)
                {
                    buffered.Flush();
                }
            }
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            bool debug = false;

            // Initialize Log4Net Logger
            XmlConfigurator.Configure();
            logger.ErrorFormat("Start @ {0}", System.DateTime.Now.ToString());

            if (e.Args.Length > 0) {
                switch (e.Args[0].Substring(0, 2).ToLower()) {
                    case "/c":
                        // User clicked the "configure" button.
                        logger.Info("/C passed");
                        ConfigureScreensaver();
                        Shutdown(0);
                        return;
                    case "/p":
                        // Previewing inside of a Win32 window specified by args[1].
                        logger.Info("/P passed");
                        ShowInParent(new IntPtr(Convert.ToInt32(e.Args[1])));
                        return;
                    case "/d":
                        // Debugging - treat mouse movement differently
                        logger.Info("/D passed");
                        debug = true;
                        break;

                    default:
                        logger.ErrorFormat("Unknown command line option {0}", e.Args[0]);
                        break;
                }
            }
            MainWindow w = new MainWindow(false);
            w.Debug = debug;
            w.Show();
        }

        // Used for Screen Saver preview
        private void ShowInParent(IntPtr parentHwnd) {
            MainWindow previewContent = new MainWindow(true);
            WindowInteropHelper windowHelper = new WindowInteropHelper(previewContent);
            windowHelper.Owner = parentHwnd;
            previewContent.WindowState = WindowState.Normal;
            RECT parentRect;
            GetClientRect(parentHwnd, out parentRect);
            previewContent.Left = 0;
            previewContent.Top = 0;
            previewContent.Width = 0;
            previewContent.Height = 0;
            previewContent.ShowInTaskbar = false;
            previewContent.ShowActivated = false;  // Doesn't work, so we'll use SetForegroundWindow() to restore focus.
            previewContent.Cursor = Cursors.Arrow;
            previewContent.ForceCursor = false;

            IntPtr currentFocus = GetForegroundWindow();
            previewContent.Show();
            SetParent(windowHelper.Handle, parentHwnd);
            SetWindowLong(windowHelper.Handle, -16, new IntPtr(0x10000000 | 0x40000000 | 0x02000000));
            previewContent.Width = parentRect.right - parentRect.left;
            previewContent.Height = parentRect.bottom - parentRect.top;
            SetForegroundWindow(currentFocus);
        }

        // Screen Saver Configure button clicked
        private void ConfigureScreensaver() {
            String videoUri = PreferenceManager.ReadVideoSettings();
            System.Windows.Forms.FolderBrowserDialog folderDialog = new System.Windows.Forms.FolderBrowserDialog();
            folderDialog.SelectedPath = videoUri;
            folderDialog.Description = "Select root folder containing pictures or videos";

            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                PreferenceManager.WriteVideoSettings(folderDialog.SelectedPath);
                logger.InfoFormat("Directory set to {0}", folderDialog.SelectedPath.ToString());
            }
        }
    }
}
