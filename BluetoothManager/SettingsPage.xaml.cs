using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using BluetoothManager.Demo_Mode;
using BluetoothManager.Services;
using Windows.System;

namespace BluetoothManager
{
    public sealed partial class SettingsPage : Page
    {
        private readonly ResourceLoader _res = new();
        private bool _loading;

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
            LicenseManager.StatusChanged += OnLicenseStatusChanged;
        }

        private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            _loading = true;
            ThemeCombo.SelectedIndex = (int)AppSettings.Theme;
            CompactToggle.IsOn = AppSettings.CompactDensity;
            SortCombo.SelectedIndex = (int)AppSettings.SortMode;
            LineEndingCombo.SelectedIndex = (int)AppSettings.LineEnding;
            SendAsHexToggle.IsOn = AppSettings.SendAsHex;
            AutoReconnectToggle.IsOn = AppSettings.AutoReconnect;
            _loading = false;

            await LicenseManager.CheckLicenseAsync();
            UpdateLicenseUi();
            RefreshLicenseButton.Content = ResHelper.Get(_res, "BtnRefreshLicense", "Restore purchases");
            PrivacyPolicyButton.Content = ResHelper.Get(_res, "BtnPrivacyPolicy", "Privacy policy");
            InitStoreScreenshotPanel();
        }

        protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await LicenseManager.CheckLicenseAsync();
            UpdateLicenseUi();
        }

        private void InitStoreScreenshotPanel()
        {
            if (!App.IsDemoMode)
            {
                StoreScreenshotPanel.Visibility = Visibility.Collapsed;
                return;
            }

            StoreScreenshotPanel.Visibility = Visibility.Visible;
            if (StoreSceneCombo.Items.Count == 0)
            {
                foreach (var scene in ScreenshotManager.StoreScenes)
                {
                    StoreSceneCombo.Items.Add(new ComboBoxItem
                    {
                        Content = scene.Label,
                        Tag = scene.FileName
                    });
                }
                StoreSceneCombo.SelectedIndex = 0;
            }
        }

        private async void CaptureStoreShot_Click(object sender, RoutedEventArgs e)
        {
            string filename = ScreenshotManager.StoreScenes.Length > 0
                ? ScreenshotManager.StoreScenes[0].FileName
                : "screenshot.png";
            if (StoreSceneCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
                filename = tag;

            CaptureStoreShotButton.IsEnabled = false;
            try
            {
                if (App.MainWindow is not MainWindow main)
                    return;

                TeachingTourHelper.SuppressTips = true;
                try
                {
                    await StoreScreenshotTour.ApplySceneAsync(main, filename);
                    main.ClearShellWarning();
                    await ScreenshotManager.CaptureWindowAsync(filename);
                }
                finally
                {
                    TeachingTourHelper.SuppressTips = false;
                }
            }
            finally
            {
                CaptureStoreShotButton.IsEnabled = true;
            }
        }

        private async void RunStoreTour_Click(object sender, RoutedEventArgs e)
        {
            await RunTourAsync(StoreScreenshotTour.DefaultLanguages);
        }

        private async void RunStoreTourAllLang_Click(object sender, RoutedEventArgs e)
        {
            await RunTourAsync(StoreScreenshotTour.AllPackageLanguages);
        }

        private async System.Threading.Tasks.Task RunTourAsync(string[] languages)
        {
            SetTourButtonsEnabled(false);
            try
            {
                int saved = await StoreScreenshotTour.CaptureAllAsync(languages);
                string folder = ScreenshotManager.GetOutputFolder();
                await DialogHelper.ShowAsync(
                    "Store tour finished",
                    $"Saved {saved} screenshots under:\n{folder}");
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowAsync("Store tour failed", ex.Message);
            }
            finally
            {
                SetTourButtonsEnabled(true);
            }
        }

        private void SetTourButtonsEnabled(bool enabled)
        {
            CaptureStoreShotButton.IsEnabled = enabled;
            RunStoreTourButton.IsEnabled = enabled;
            RunStoreTourAllLangButton.IsEnabled = enabled;
        }

        private void UpdateLicenseUi()
        {
            LicenseStatusText.Text = LicenseManager.AccessKind switch
            {
                ProAccessKind.Purchased => ResHelper.Get(_res, "LicenseStatusPro", "License: BlueIDE PRO"),
                ProAccessKind.Trial => string.Format(
                    ResHelper.Get(_res, "LicenseStatusTrial", "License: PRO trial ({0} day(s) left)"),
                    LicenseManager.TrialDaysRemaining),
                _ => ResHelper.Get(_res, "LicenseStatusFree", "License: Free (trial ended)")
            };
            BuyProSettingsButton.Content = ResHelper.Get(_res, "BtnBuyPro", "Buy PRO");
            BuyProSettingsButton.Visibility = LicenseManager.AccessKind == ProAccessKind.Purchased
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void OnLicenseStatusChanged()
        {
            DispatcherQueue.TryEnqueue(UpdateLicenseUi);
        }

        private async void RefreshLicense_Click(object sender, RoutedEventArgs e)
        {
            RefreshLicenseButton.IsEnabled = false;
            await LicenseManager.CheckLicenseAsync();
            UpdateLicenseUi();

            string content = LicenseManager.IsProUser
                ? ResHelper.Get(_res, "LicenseRestoreFound", "Your BlueIDE PRO purchase is active on this device.")
                : ResHelper.Get(_res, "LicenseRestoreNotFound", "No active BlueIDE PRO purchase was found for this Microsoft account on this device.");

            await DialogHelper.ShowAsync(
                ResHelper.Get(_res, "LicenseRestoreTitle", "Restore purchases"),
                content);
            RefreshLicenseButton.IsEnabled = true;
        }

        private async void BuyPro_Click(object sender, RoutedEventArgs e)
        {
            BuyProSettingsButton.IsEnabled = false;
            bool success = await LicenseManager.RequestPurchaseAsync();
            if (success)
            {
                await DialogHelper.ShowAsync(
                    ResHelper.Get(_res, "ProPurchaseSuccessTitle", "Success!"),
                    ResHelper.Get(_res, "ProPurchaseSuccessContent", "PRO unlocked."));
                await RateManager.RegisterMeaningfulEngageAsync();
            }
            else
            {
                await DialogHelper.ShowAsync(
                    ResHelper.Get(_res, "ProPurchaseFailTitle", "Purchase Error"),
                    ResHelper.Get(_res, "ProPurchaseFailContent", "Failed to complete the purchase."));
            }
            UpdateLicenseUi();
            BuyProSettingsButton.IsEnabled = true;
        }

        private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || ThemeCombo.SelectedIndex < 0) return;
            AppSettings.Theme = (AppThemeMode)ThemeCombo.SelectedIndex;
            ThemeHelper.ApplyTheme(AppSettings.Theme);
            AppSettings.NotifyChanged();
        }

        private void CompactToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            AppSettings.CompactDensity = CompactToggle.IsOn;
            AppSettings.NotifyChanged();
        }

        private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || SortCombo.SelectedIndex < 0) return;
            AppSettings.SortMode = (DeviceSortMode)SortCombo.SelectedIndex;
            AppSettings.NotifyChanged();
        }

        private void LineEndingCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || LineEndingCombo.SelectedIndex < 0) return;
            AppSettings.LineEnding = (LineEndingMode)LineEndingCombo.SelectedIndex;
            AppSettings.NotifyChanged();
        }

        private void SendAsHexToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            AppSettings.SendAsHex = SendAsHexToggle.IsOn;
            AppSettings.NotifyChanged();
        }

        private void AutoReconnectToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            AppSettings.AutoReconnect = AutoReconnectToggle.IsOn;
            AppSettings.NotifyChanged();
        }

        private async void ExportPresets_Click(object sender, RoutedEventArgs e)
        {
            bool ok = await CommandPresetStore.ExportPresetsAsync();
            if (ok)
                await DialogHelper.ShowAsync(_res.GetString("ExportSuccessTitle"), _res.GetString("ExportSuccessContent"));
        }

        private async void ImportPresets_Click(object sender, RoutedEventArgs e)
        {
            bool ok = await CommandPresetStore.ImportPresetsAsync();
            if (ok)
                await DialogHelper.ShowAsync(_res.GetString("ImportSuccessTitle"), _res.GetString("ImportSuccessContent"));
        }

        private async void RateApp_Click(object sender, RoutedEventArgs e)
        {
            await RateManager.PromptRatingAsync(force: true);
        }

        private async void PrivacyPolicy_Click(object sender, RoutedEventArgs e)
        {
            var uri = new Uri("https://apps.microsoft.com/detail/9N5H75QWBHLL");
            bool launched = await Launcher.LaunchUriAsync(uri);
            if (!launched)
            {
                await DialogHelper.ShowAsync(
                    ResHelper.Get(_res, "PrivacyPolicyTitle", "Privacy policy"),
                    ResHelper.Get(_res, "PrivacyPolicyStoreFallback",
                        "Open the BlueIDE Microsoft Store page and add your public privacy policy URL in Partner Center before the next submission."));
            }
        }

        private async void ShowOnboarding_Click(object sender, RoutedEventArgs e)
        {
            TeachingTourHelper.ResetForReplay();
            await OnboardingHelper.ShowAsync(force: true);
            if (App.MainWindow is MainWindow main)
                main.NavigateToDevicesForTour();
        }
    }
}
