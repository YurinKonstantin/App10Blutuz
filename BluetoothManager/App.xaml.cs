using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace BluetoothManager
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;
        // 1. Создаем публичное статическое свойство для доступа из любого места кода
        public static Window MainWindow { get; set; }
        // Глобальный загрузчик ресурсов для доступа из моделей данных
        public static Microsoft.Windows.ApplicationModel.Resources.ResourceLoader ResLoader { get; private set; }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
            // Инициализируем загрузчик ресурсов при старте приложения
            ResLoader = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader();

        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Включите true только на время генерации скриншотов!
          
          
            //_window = new MainWindow();
            //_window.Activate();
            // 2. Инициализируем наше статическое свойство вместо локальной переменной
            MainWindow = new MainWindow();


            // Включите true только на время генерации скриншотов!
            bool isDemoMode = false;

            if (isDemoMode)
            {
                // Принудительно задаем размер окна 1920x1080 для идеальных скриншотов Microsoft Store
                MainWindow.Activate();

                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow);
                Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                if (appWindow != null)
                {
                    // Ресайзим окно с учетом рамок Windows под чистый формат Full HD
                    appWindow.Resize(new Windows.Graphics.SizeInt32(1440, 810));
                }
            }
            else
            {
                MainWindow.Activate();
            }
        }
    }
}
