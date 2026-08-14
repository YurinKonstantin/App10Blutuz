using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace BluetoothManager.Services
{
    public enum TrafficDirection
    {
        Tx,
        Rx,
        Info
    }

    public sealed class TrafficLogEntry : INotifyPropertyChanged
    {
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
        public TrafficDirection Direction { get; init; }
        public string Message { get; init; } = "";
        public int ByteCount { get; init; }

        public string TimestampString => Timestamp.ToString("HH:mm:ss.fff");
        public string DirectionTag => Direction switch
        {
            TrafficDirection.Tx => "TX",
            TrafficDirection.Rx => "RX",
            _ => "··"
        };

        public string DisplayLine => $"[{TimestampString}] [{DirectionTag}] {Message}";

        public Brush DirectionBrush => Direction switch
        {
            TrafficDirection.Tx => ThemeHelper.GetBrush("TxForegroundBrush"),
            TrafficDirection.Rx => ThemeHelper.GetBrush("RxForegroundBrush"),
            _ => ThemeHelper.GetBrush("InfoForegroundBrush")
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class TrafficLogStore
    {
        public ObservableCollection<TrafficLogEntry> Entries { get; } = new();
        public long TotalBytesTx { get; private set; }
        public long TotalBytesRx { get; private set; }

        public event Action? Changed;

        public void Append(TrafficDirection direction, string message, int byteCount = 0)
        {
            var entry = new TrafficLogEntry
            {
                Direction = direction,
                Message = message.TrimEnd('\r', '\n'),
                ByteCount = byteCount,
                Timestamp = DateTimeOffset.Now
            };

            if (direction == TrafficDirection.Tx) TotalBytesTx += byteCount;
            if (direction == TrafficDirection.Rx) TotalBytesRx += byteCount;

            Entries.Add(entry);
            if (Entries.Count > 5000)
                Entries.RemoveAt(0);

            Changed?.Invoke();
        }

        public void Clear()
        {
            Entries.Clear();
            TotalBytesTx = 0;
            TotalBytesRx = 0;
            Changed?.Invoke();
        }

        public string ToPlainText()
        {
            var sb = new StringBuilder();
            foreach (var e in Entries)
                sb.AppendLine(e.DisplayLine);
            return sb.ToString();
        }

        public string ToCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,Direction,Bytes,Message");
            foreach (var e in Entries)
            {
                string msg = e.Message.Replace("\"", "\"\"");
                sb.AppendLine($"\"{e.Timestamp:O}\",{e.DirectionTag},{e.ByteCount},\"{msg}\"");
            }
            return sb.ToString();
        }

        public async Task<bool> ExportAsync(bool csv)
        {
            var picker = new FileSavePicker();
            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            if (csv)
            {
                picker.FileTypeChoices.Add("CSV", new[] { ".csv" });
                picker.SuggestedFileName = $"blueide-log-{DateTime.Now:yyyyMMdd-HHmmss}";
            }
            else
            {
                picker.FileTypeChoices.Add("Text", new[] { ".txt" });
                picker.SuggestedFileName = $"blueide-log-{DateTime.Now:yyyyMMdd-HHmmss}";
            }

            StorageFile? file = await picker.PickSaveFileAsync();
            if (file == null) return false;

            await FileIO.WriteTextAsync(file, csv ? ToCsv() : ToPlainText());
            return true;
        }
    }
}
