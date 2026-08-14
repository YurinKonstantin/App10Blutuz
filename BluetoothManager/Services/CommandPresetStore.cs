using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace BluetoothManager.Services
{
    public sealed class CommandPreset
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public string Payload { get; set; } = "";
        public bool AsHex { get; set; }
        public int DelayMs { get; set; }
    }

    public static class CommandPresetStore
    {
        private const string Key = "CommandPresets_v1";
        private const string HistoryKey = "CommandHistory_v1";
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

        public static List<CommandPreset> LoadPresets()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            if (!values.TryGetValue(Key, out var raw) || raw is not string json || string.IsNullOrWhiteSpace(json))
                return new List<CommandPreset>();

            try
            {
                return JsonSerializer.Deserialize<List<CommandPreset>>(json, JsonOptions) ?? new List<CommandPreset>();
            }
            catch
            {
                return new List<CommandPreset>();
            }
        }

        public static void SavePresets(IEnumerable<CommandPreset> presets)
        {
            ApplicationData.Current.LocalSettings.Values[Key] =
                JsonSerializer.Serialize(presets.ToList(), JsonOptions);
        }

        public static List<string> LoadHistory()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            if (!values.TryGetValue(HistoryKey, out var raw) || raw is not string json)
                return new List<string>();
            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        public static void PushHistory(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;
            var list = LoadHistory();
            list.RemoveAll(x => string.Equals(x, command, StringComparison.Ordinal));
            list.Insert(0, command);
            if (list.Count > 40) list = list.Take(40).ToList();
            ApplicationData.Current.LocalSettings.Values[HistoryKey] = JsonSerializer.Serialize(list);
        }

        public static async Task<bool> ExportPresetsAsync()
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("JSON", new[] { ".json" });
            picker.SuggestedFileName = "blueide-presets";

            var file = await picker.PickSaveFileAsync();
            if (file == null) return false;

            await FileIO.WriteTextAsync(file, JsonSerializer.Serialize(LoadPresets(), new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }

        public static async Task<bool> ImportPresetsAsync()
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".json");

            var file = await picker.PickSingleFileAsync();
            if (file == null) return false;

            string json = await FileIO.ReadTextAsync(file);
            var imported = JsonSerializer.Deserialize<List<CommandPreset>>(json) ?? new List<CommandPreset>();
            var existing = LoadPresets();
            foreach (var p in imported)
            {
                if (existing.All(e => e.Id != p.Id))
                    existing.Add(p);
            }
            SavePresets(existing);
            return true;
        }
    }
}
