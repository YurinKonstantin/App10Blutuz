using System;
using Microsoft.Windows.ApplicationModel.Resources;

namespace BluetoothManager
{
    /// <summary>Safe resource lookup — ResourceLoader.GetString throws COMException when a key is missing.</summary>
    public static class ResHelper
    {
        public static string Get(string key, string fallback = "")
        {
            var loader = App.ResLoader ?? new ResourceLoader();
            return Get(loader, key, fallback);
        }

        public static string Get(ResourceLoader? loader, string key, string fallback = "")
        {
            if (loader == null) return Get(key, fallback);

            string? value = TryGet(loader, key);
            if (!string.IsNullOrEmpty(value)) return value;

            // x:Uid keys live as "BtnFoo.Content"; code often looks up "BtnFoo".
            if (!key.Contains('.', StringComparison.Ordinal))
            {
                value = TryGet(loader, key + ".Content");
                if (!string.IsNullOrEmpty(value)) return value;
            }

            return fallback;
        }

        private static string? TryGet(ResourceLoader loader, string key)
        {
            try
            {
                return loader.GetString(key);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
