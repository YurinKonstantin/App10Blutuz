using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using WinRT.Interop;

namespace BluetoothManager
{
    public sealed partial class MainWindow : Window
    {
        private ResourceLoader _resourceLoader = new();
        private AppWindow? _appWindow;
        private string _currentTag = "";
        private bool _navReady;

        public MainWindow()
        {
            InitializeComponent();
            App.MainWindow = this;
            ConnectionStatusHub.Changed += OnConnectionStatusChanged;
            UpdateShellConnectionStatus();
            SetWindowIconAndTitle();

            // Defer first navigation until Activated so XamlRoot / resources are ready
            // (avoids COMException on devices without Bluetooth during early dialog/resource access).
            Activated += MainWindow_FirstActivated;
        }

        private void MainWindow_FirstActivated(object sender, WindowActivatedEventArgs args)
        {
            Activated -= MainWindow_FirstActivated;
            _navReady = true;
            if (MainNav.MenuItems.Count > 0)
                MainNav.SelectedItem = MainNav.MenuItems[0];
            DispatcherQueue.TryEnqueue(async () =>
            {
                await LicenseManager.CheckLicenseAsync();
                await OnboardingHelper.ShowAsync();
            });
        }

        private void SetWindowIconAndTitle()
        {
            IntPtr windowHandle = WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            if (_appWindow != null)
            {
                _appWindow.Title = ResHelper.Get(_resourceLoader, "AppTitle", "BlueIDE");
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "appicon.ico");
                if (System.IO.Path.Exists(iconPath))
                    _appWindow.SetIcon(iconPath);
            }
        }

        private void OnConnectionStatusChanged()
        {
            DispatcherQueue.TryEnqueue(UpdateShellConnectionStatus);
        }

        private void UpdateShellConnectionStatus()
        {
            if (ConnectionStatusHub.IsConnected)
            {
                ShellConnectionDot.Fill = ThemeHelper.GetBrush("StatusConnectedBrush");
                string name = ConnectionStatusHub.DeviceName ?? "";
                ShellConnectionText.Text = string.IsNullOrEmpty(name)
                    ? ResHelper.Get(_resourceLoader, "ShellStatusConnected", "Connected")
                    : $"{ResHelper.Get(_resourceLoader, "ShellStatusConnectedTo", "Connected:")} {name}";
            }
            else
            {
                ShellConnectionDot.Fill = ThemeHelper.GetBrush("StatusDisconnectedBrush");
                ShellConnectionText.Text = ResHelper.Get(_resourceLoader, "ShellStatusDisconnected", "Disconnected");
            }
        }

        private void NavigateTo(Type pageType)
        {
            if (ContentFrame.Content?.GetType() == pageType)
                return;
            ContentFrame.Navigate(pageType);
        }

        /// <summary>Selects Devices in the nav and starts the TeachingTip tour after layout.</summary>
        public void NavigateToDevicesForTour()
        {
            if (MainNav.MenuItems.Count > 0)
                MainNav.SelectedItem = MainNav.MenuItems[0];

            DispatcherQueue.TryEnqueue(async () =>
            {
                await TeachingTourHelper.DelayForLayoutAsync();
                if (ContentFrame.Content is DevicesPage devices)
                    devices.StartTeachingTour();
            });
        }

        /// <summary>Current page in the content frame (for demo / screenshot tour).</summary>
        public object? CurrentPage => ContentFrame.Content;

        /// <summary>Selects a primary nav item by Tag (e.g. devices, analyzer).</summary>
        public void NavigateToTag(string tag)
        {
            foreach (var item in MainNav.MenuItems)
            {
                if (item is NavigationViewItem nvi && nvi.Tag as string == tag)
                {
                    MainNav.SelectedItem = nvi;
                    return;
                }
            }
        }

        /// <summary>
        /// Navigates to a nav tag with cache disabled so pages reload after a language change.
        /// </summary>
        public void ForceNavigateFresh(string tag)
        {
            ContentFrame.CacheSize = 0;
            if (ContentFrame.Content is Page current)
                current.NavigationCacheMode = NavigationCacheMode.Disabled;

            Type? pageType = tag switch
            {
                "analyzer" => typeof(AnalyzerPage),
                "devices" => typeof(DevicesPage),
                "settings" => typeof(SettingsPage),
                _ => typeof(DevicesPage)
            };

            // Select nav item without relying on SelectionChanged alone (cache bust)
            foreach (var item in MainNav.MenuItems)
            {
                if (item is NavigationViewItem nvi && nvi.Tag as string == tag)
                {
                    MainNav.SelectedItem = nvi;
                    break;
                }
            }

            ContentFrame.Navigate(pageType, null, new SuppressNavigationTransitionInfo());
            if (ContentFrame.Content is Page next)
                next.NavigationCacheMode = NavigationCacheMode.Disabled;

            PageHeader.Text = tag switch
            {
                "analyzer" => ResHelper.Get(_resourceLoader, "NavAnalyzer.Content", "LE Analyzer"),
                "settings" => ResHelper.Get(_resourceLoader, "SettingsTitle.Text", "Settings"),
                _ => ResHelper.Get(_resourceLoader, "NavDevices.Content", "Devices")
            };
            _currentTag = tag;
        }

        /// <summary>Refreshes shell chrome strings after PrimaryLanguageOverride change.</summary>
        public void RefreshShellLocalization()
        {
            _resourceLoader = new ResourceLoader();
            if (_appWindow != null)
                _appWindow.Title = ResHelper.Get(_resourceLoader, "AppTitle", "BlueIDE");
            UpdateShellConnectionStatus();
            PageHeader.Text = _currentTag switch
            {
                "analyzer" => ResHelper.Get(_resourceLoader, "NavAnalyzer.Content", "LE Analyzer"),
                "settings" => ResHelper.Get(_resourceLoader, "SettingsTitle.Text", "Settings"),
                _ => ResHelper.Get(_resourceLoader, "NavDevices.Content", "Devices")
            };
        }

        /// <summary>Shows a non-blocking shell warning (e.g. no Bluetooth hardware).</summary>
        public void ShowShellWarning(string title, string message)
        {
            if (TeachingTourHelper.SuppressTips || Demo_Mode.StoreScreenshotTour.IsRunning)
            {
                DisconnectWarningBar.IsOpen = false;
                return;
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                DisconnectWarningBar.Severity = InfoBarSeverity.Warning;
                DisconnectWarningBar.Title = title;
                DisconnectWarningBar.Message = message;
                DisconnectWarningBar.IsOpen = true;
            });
        }

        public void ClearShellWarning()
        {
            DispatcherQueue.TryEnqueue(() => DisconnectWarningBar.IsOpen = false);
        }

        private void MainNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (!_navReady) return;

            string nextTag = args.IsSettingsSelected
                ? "settings"
                : args.SelectedItemContainer?.Tag as string ?? "";

            _currentTag = nextTag;

            try
            {
                if (args.IsSettingsSelected)
                {
                    PageHeader.Text = ResHelper.Get(_resourceLoader, "SettingsTitle.Text", "Settings");
                    NavigateTo(typeof(SettingsPage));
                    return;
                }

                if (args.SelectedItemContainer?.Tag is string tag)
                {
                    switch (tag)
                    {
                        case "devices":
                            PageHeader.Text = ResHelper.Get(_resourceLoader, "NavDevices.Content", "Devices");
                            NavigateTo(typeof(DevicesPage));
                            break;
                        case "analyzer":
                            PageHeader.Text = ResHelper.Get(_resourceLoader, "NavAnalyzer.Content", "LE Analyzer");
                            NavigateTo(typeof(AnalyzerPage));
                            break;
                    }
                }
            }
            catch (Exception)
            {
                PageHeader.Text = nextTag switch
                {
                    "analyzer" => "LE Analyzer",
                    "settings" => "Settings",
                    _ => "Devices"
                };
            }
        }
    }
}
