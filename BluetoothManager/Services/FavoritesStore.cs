using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Windows.Storage;

namespace BluetoothManager.Services
{
    public static class FavoritesStore
    {
        private const string Key = "FavoriteDeviceIds_v1";

        public static HashSet<string> Load()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            if (!values.TryGetValue(Key, out var raw) || raw is not string json)
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                return new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public static void Save(IEnumerable<string> ids)
        {
            ApplicationData.Current.LocalSettings.Values[Key] =
                JsonSerializer.Serialize(ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        }

        public static bool Toggle(string deviceId)
        {
            var set = Load();
            bool added;
            if (set.Contains(deviceId))
            {
                set.Remove(deviceId);
                added = false;
            }
            else
            {
                set.Add(deviceId);
                added = true;
            }
            Save(set);
            return added;
        }

        public static bool Contains(string deviceId) => Load().Contains(deviceId);
    }
}
