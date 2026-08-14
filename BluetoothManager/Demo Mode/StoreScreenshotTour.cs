using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Globalization;

namespace BluetoothManager.Demo_Mode
{
    /// <summary>
    /// Automated Store screenshot tour: seeds demo data, walks all scenes,
    /// switches language, and captures again.
    /// </summary>
    public static class StoreScreenshotTour
    {
        /// <summary>Default pair: English + Russian (second language pass).</summary>
        public static readonly string[] DefaultLanguages = { "en-US", "ru-RU" };

        public static readonly string[] AllPackageLanguages =
        {
            "en-US", "ru-RU", "de-DE", "es-ES", "fr-FR", "ja-JP", "pt-BR", "zh-CN"
        };

        public static bool IsRunning { get; private set; }

        public static async Task<int> CaptureAllAsync(IReadOnlyList<string>? languages = null)
        {
            languages ??= DefaultLanguages;
            if (App.MainWindow is not MainWindow main)
                return 0;

            IsRunning = true;
            TeachingTourHelper.SuppressTips = true;
            int saved = 0;
            string previousLang = ApplicationLanguages.PrimaryLanguageOverride ?? "";

            try
            {
                foreach (string lang in languages)
                {
                    await SwitchLanguageAsync(lang, main);

                    foreach (var scene in ScreenshotManager.StoreScenes)
                    {
                        await ApplySceneAsync(main, scene.FileName);
                        await Task.Delay(500);
                        main.ClearShellWarning();

                        string relativePath = $"{lang}/{scene.FileName}";
                        string? path = await ScreenshotManager.CaptureWindowAsync(relativePath);
                        if (path != null)
                            saved++;

                        await Task.Delay(200);
                    }
                }
            }
            finally
            {
                TeachingTourHelper.SuppressTips = false;
                IsRunning = false;
                try
                {
                    if (!string.IsNullOrEmpty(previousLang))
                        await SwitchLanguageAsync(previousLang, main);
                    else
                    {
                        ApplicationLanguages.PrimaryLanguageOverride = "";
                        App.ResLoader = new ResourceLoader();
                    }
                }
                catch
                {
                    // best-effort restore
                }
            }

            return saved;
        }

        public static async Task ApplySceneAsync(MainWindow main, string sceneFileName)
        {
            string navTag = IsAnalyzerScene(sceneFileName) ? "analyzer" : "devices";
            main.ForceNavigateFresh(navTag);
            await TeachingTourHelper.DelayForLayoutAsync();

            // Page load may flash the "no Bluetooth adapter" shell warning — dismiss before seeding
            main.ClearShellWarning();

            if (IsAnalyzerScene(sceneFileName))
            {
                if (main.CurrentPage is AnalyzerPage analyzer)
                    analyzer.ApplyDemoScene(sceneFileName);
            }
            else
            {
                if (main.CurrentPage is DevicesPage devices)
                    devices.ApplyDemoScene(sceneFileName);
            }

            main.ClearShellWarning();
            await Task.Delay(250);
            main.ClearShellWarning();
        }

        public static async Task SwitchLanguageAsync(string bcp47, MainWindow main)
        {
            ApplicationLanguages.PrimaryLanguageOverride = bcp47;
            try
            {
                CultureInfo.CurrentUICulture = new CultureInfo(bcp47);
                CultureInfo.CurrentCulture = new CultureInfo(bcp47);
            }
            catch
            {
                // culture parse may fail for some tags; PrimaryLanguageOverride still applies
            }

            App.ResLoader = new ResourceLoader();
            main.RefreshShellLocalization();

            main.ForceNavigateFresh("devices");
            await TeachingTourHelper.DelayForLayoutAsync();
        }

        private static bool IsAnalyzerScene(string fileName)
            => fileName.StartsWith("04-", StringComparison.Ordinal)
               || fileName.StartsWith("06-", StringComparison.Ordinal);
    }
}
