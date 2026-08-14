using System;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace BluetoothManager
{
    public enum AppThemeMode
    {
        System = 0,
        Light = 1,
        Dark = 2
    }

    public enum DeviceSortMode
    {
        Rssi = 0,
        Name = 1,
        RecentlySeen = 2
    }

    public enum LineEndingMode
    {
        Lf = 0,
        CrLf = 1,
        Cr = 2,
        None = 3
    }

    /// <summary>Persisted user preferences (LocalSettings).</summary>
    public static class AppSettings
    {
        private static IPropertySet Values => ApplicationData.Current.LocalSettings.Values;

        public static AppThemeMode Theme
        {
            get => (AppThemeMode)GetInt("Theme", (int)AppThemeMode.System);
            set => Values["Theme"] = (int)value;
        }

        public static bool CompactDensity
        {
            get => GetBool("CompactDensity", false);
            set => Values["CompactDensity"] = value;
        }

        public static DeviceSortMode SortMode
        {
            get => (DeviceSortMode)GetInt("SortMode", (int)DeviceSortMode.Rssi);
            set => Values["SortMode"] = (int)value;
        }

        public static LineEndingMode LineEnding
        {
            get => (LineEndingMode)GetInt("LineEnding", (int)LineEndingMode.Lf);
            set => Values["LineEnding"] = (int)value;
        }

        public static bool SendAsHex
        {
            get => GetBool("SendAsHex", false);
            set => Values["SendAsHex"] = value;
        }

        public static bool AutoReconnect
        {
            get => GetBool("AutoReconnect", false);
            set => Values["AutoReconnect"] = value;
        }

        public static bool OnboardingCompleted
        {
            get => GetBool("OnboardingCompleted", false);
            set => Values["OnboardingCompleted"] = value;
        }

        public static bool TeachingTipsCompleted
        {
            get => GetBool("TeachingTipsCompleted", false);
            set => Values["TeachingTipsCompleted"] = value;
        }

        public static bool AnalyzerTipShown
        {
            get => GetBool("AnalyzerTipShown", false);
            set => Values["AnalyzerTipShown"] = value;
        }

        /// <summary>ISO-8601 UTC when the PRO trial started; empty until first license check.</summary>
        public static string TrialStartedUtc
        {
            get => Values.TryGetValue("TrialStartedUtc", out var v) ? v?.ToString() ?? "" : "";
            set => Values["TrialStartedUtc"] = value ?? "";
        }

        public static string LastConnectedDeviceId
        {
            get => Values.TryGetValue("LastConnectedDeviceId", out var v) ? v?.ToString() ?? "" : "";
            set => Values["LastConnectedDeviceId"] = value ?? "";
        }

        public static string GetLineEndingString()
        {
            return LineEnding switch
            {
                LineEndingMode.CrLf => "\r\n",
                LineEndingMode.Cr => "\r",
                LineEndingMode.None => "",
                _ => "\n"
            };
        }

        public static event EventHandler? SettingsChanged;

        public static void NotifyChanged() => SettingsChanged?.Invoke(null, EventArgs.Empty);

        private static int GetInt(string key, int fallback)
            => Values.TryGetValue(key, out var v) && v is int i ? i : fallback;

        private static bool GetBool(string key, bool fallback)
            => Values.TryGetValue(key, out var v) && v is bool b ? b : fallback;
    }
}
