using Microsoft.UI.Xaml;
using System;

namespace BluetoothManager
{
    public partial class App : Application
    {
        public static Window MainWindow { get; set; } = null!;
        public static Microsoft.Windows.ApplicationModel.Resources.ResourceLoader ResLoader { get; set; } = null!;

        /// <summary>True in Debug builds — fixed Store screenshot window size.</summary>
        public static bool IsDemoMode
        {
            get
            {
#if DEBUG
                return true;
#else
                return false;
#endif
            }
        }

        public App()
        {
            InitializeComponent();
            ResLoader = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();
            ThemeHelper.ApplyTheme(AppSettings.Theme);
            MainWindow.Activate();

            if (IsDemoMode)
            {
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow);
                Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                // Typical Microsoft Store desktop screenshot size
                appWindow?.Resize(new Windows.Graphics.SizeInt32(1366, 768));
            }
        }
    }
}
