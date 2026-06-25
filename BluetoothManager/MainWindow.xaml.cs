using BluetoothManager.Demo_Mode;
using Microsoft.UI.Windowing; // Обязательно для AppWindow
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace BluetoothManager
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private readonly ResourceLoader _resourceLoader = new ResourceLoader();

        private AppWindow _appWindow;
        public MainWindow()
        {
            this.InitializeComponent();
            // Задаем корневой элемент для XamlRoot диалоговых окон
            //  App.MainWindow = this;
            App.MainWindow = this;
            // При старте автоматически открываем первую вкладку (Устройства)
            MainNav.SelectedItem = MainNav.MenuItems[0];
            // Настраиваем локализованный заголовок и системную иконку
            SetWindowIconAndTitle();
        }
        /// <summary>
        /// Устанавливает мультиязычный заголовок и привязывает иконку к окну WinUI 3
        /// </summary>
        private void SetWindowIconAndTitle()
        {
            // 1. Получаем HWND (дескриптор) текущего окна
            IntPtr windowHandle = WindowNative.GetWindowHandle(this);

            // 2. Получаем WindowId из HWND
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);

            // 3. Получаем объект AppWindow, управляющий системной рамкой окна
            _appWindow = AppWindow.GetFromWindowId(windowId);

            if (_appWindow != null)
            {
                // 4. Задаем локализованный заголовок программы из Resources.resw
                _appWindow.Title = _resourceLoader.GetString("AppTitle") ?? "BlueIDE";

                // 5. Загружаем иконку. Файл AppIcon.ico должен лежать в папке Assets
                // AppDomain.CurrentDomain.BaseDirectory ведет в корень папки сборки приложения
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "appicon.ico");

                if (System.IO.Path.Exists(iconPath))
                {
                    _appWindow.SetIcon(iconPath);
                }
            }
        }


        private void MainNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer != null)
            {
                string tag = args.SelectedItemContainer.Tag.ToString();

                switch (tag)
                {
                    case "devices":
                        // Переходим на страницу устройств (создадим её на Шаге 3)
                        ContentFrame.Navigate(typeof(DevicesPage));
                        break;

                    case "analyzer":
                        // Переходим на страницу анализатора (создадим её на Шаге 4)
                        ContentFrame.Navigate(typeof(AnalyzerPage));
                        break;
                }
            }
        }

        private int _screenshotStep = 1;



        //private async void CaptureScreenshot_Click(object sender, RoutedEventArgs e)
        //{
        //    // Скрываем кнопку, чтобы она не попала в кадр
        //    if (sender is Button btn)
        //    {
        //        btn.Visibility = Visibility.Collapsed;

        //        if (ContentFrame.Content is DevicesPage devicesPage)
        //        {
        //            if (_screenshotStep == 1)
        //            {
        //                // 🎬 КАДР 1: Сканер, Радар и Радиоэфир
        //                devicesPage.LoadDemoDataForScreenshots(1);
        //                devicesPage.UpdateLayout();
        //                await System.Threading.Tasks.Task.Delay(1200);

        //                await ScreenshotManager.CaptureUiAsync(this.Content as FrameworkElement, "Store_Screen_1_Scanner.png");

        //                _screenshotStep = 2;
        //            }
        //            else if (_screenshotStep == 2)
        //            {
        //                // 🎬 КАДР 2: Активный Терминал (Клиент)
        //                devicesPage.LoadDemoDataForScreenshots(2);
        //                devicesPage.UpdateLayout();
        //                await System.Threading.Tasks.Task.Delay(1200);

        //                await ScreenshotManager.CaptureUiAsync(this.Content as FrameworkElement, "Store_Screen_2_Terminal.png");

        //                _screenshotStep = 3;
        //            }
        //            else if (_screenshotStep == 3)
        //            {
        //                // 🎬 КАДР 3: Bluetooth-Сервер (PRO)
        //                devicesPage.LoadDemoDataForScreenshots(3);
        //                devicesPage.UpdateLayout();
        //                await System.Threading.Tasks.Task.Delay(1200);

        //                await ScreenshotManager.CaptureUiAsync(this.Content as FrameworkElement, "Store_Screen_3_Server.png");

        //                _screenshotStep = 4;

        //                // Автоматически перелистываем боковую панель на Анализатор для финального кадра
        //                MainNav.SelectedItem = MainNav.MenuItems[1]; // Индекс вкладки Анализатора
        //            }
        //        }
        //        else if (ContentFrame.Content is AnalyzerPage analyzerPage)
        //        {
        //            if (_screenshotStep == 4)
        //            {
        //                // 🎬 КАДР 4: Низкоуровневый GATT Анализатор (PRO)
        //                analyzerPage.LoadDemoDataForAnalyzerScreenshot();
        //                analyzerPage.UpdateLayout();
        //                await System.Threading.Tasks.Task.Delay(1200);

        //                await ScreenshotManager.CaptureUiAsync(this.Content as FrameworkElement, "Store_Screen_4_Analyzer.png");

        //                _screenshotStep = 1; // Возвращаем цикл в начало

        //                // Возвращаем боковое меню на первую вкладку
        //                MainNav.SelectedItem = MainNav.MenuItems[0];
        //            }
        //        }

        //        if (btn != null) btn.Visibility = Visibility.Visible;
        //    }
        //}


    }

}
