using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace BluetoothManager
{
    public static class ThemeHelper
    {
        public static void ApplyTheme(AppThemeMode mode)
        {
            if (App.MainWindow?.Content is not FrameworkElement root) return;

            root.RequestedTheme = mode switch
            {
                AppThemeMode.Light => ElementTheme.Light,
                AppThemeMode.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }

        /// <summary>
        /// Resolves a brush from ThemeDictionaries using the window content ActualTheme.
        /// </summary>
        public static Brush GetBrush(string key)
        {
            var theme = (App.MainWindow?.Content as FrameworkElement)?.ActualTheme
                        ?? ElementTheme.Default;

            string dictKey = theme switch
            {
                ElementTheme.Light => "Light",
                ElementTheme.Dark => "Default",
                _ => Application.Current.RequestedTheme == ApplicationTheme.Light ? "Light" : "Default"
            };

            if (TryGetFromThemeDictionaries(Application.Current.Resources, dictKey, key, out Brush? brush))
                return brush!;

            foreach (var merged in Application.Current.Resources.MergedDictionaries)
            {
                if (TryGetFromThemeDictionaries(merged, dictKey, key, out brush))
                    return brush!;
            }

            return (Brush)Application.Current.Resources[key];
        }

        private static bool TryGetFromThemeDictionaries(
            ResourceDictionary resources, string themeKey, string brushKey, out Brush? brush)
        {
            brush = null;
            if (!resources.ThemeDictionaries.TryGetValue(themeKey, out object? dictObj))
                return false;
            if (dictObj is not ResourceDictionary themeDict)
                return false;
            if (!themeDict.TryGetValue(brushKey, out object? value) || value is not Brush found)
                return false;
            brush = found;
            return true;
        }
    }

    public static class DialogHelper
    {
        public static async System.Threading.Tasks.Task ShowAsync(string title, string content)
        {
            try
            {
                if (App.MainWindow?.Content?.XamlRoot == null) return;

                var dialog = new ContentDialog
                {
                    Title = title ?? "",
                    Content = content ?? "",
                    CloseButtonText = ResHelper.Get("DialogOk", "OK"),
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception)
            {
                // Ignore: dialog may fail if XamlRoot is not ready yet (e.g. pre-Activate).
            }
        }
    }
}
