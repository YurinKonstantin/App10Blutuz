using System;
using System.Globalization;
using System.Linq;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Globalization;
using Windows.System.UserProfile;

namespace BluetoothManager
{
    /// <summary>
    /// Maps Windows preferred languages onto packaged .resw locales.
    /// WASDK ResourceLoader otherwise stays on the package default (en-US) in Release.
    /// </summary>
    public static class LocalizationHelper
    {
        public static readonly string[] SupportedLanguages =
        {
            "en-US", "ru-RU", "de-DE", "es-ES", "fr-FR", "ja-JP", "pt-BR", "zh-CN"
        };

        public static string ResolveWindowsLanguage()
        {
            foreach (string preferred in GlobalizationPreferences.Languages)
            {
                string? exact = SupportedLanguages.FirstOrDefault(s =>
                    s.Equals(preferred, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                    return exact;

                string prefix = preferred.Split('-', '_')[0];
                string? byPrefix = SupportedLanguages.FirstOrDefault(s =>
                    s.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                if (byPrefix != null)
                    return byPrefix;
            }

            return "en-US";
        }

        public static void ApplyWindowsLanguage()
        {
            ApplyLanguage(ResolveWindowsLanguage());
        }

        public static void ApplyLanguage(string bcp47)
        {
            if (string.IsNullOrWhiteSpace(bcp47))
                bcp47 = "en-US";

            ApplicationLanguages.PrimaryLanguageOverride = bcp47;

            try
            {
                var culture = new CultureInfo(bcp47);
                CultureInfo.CurrentUICulture = culture;
                CultureInfo.CurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
                CultureInfo.DefaultThreadCurrentCulture = culture;
            }
            catch
            {
                // tag still applied via PrimaryLanguageOverride
            }

            App.ResLoader = new ResourceLoader();
        }
    }
}
