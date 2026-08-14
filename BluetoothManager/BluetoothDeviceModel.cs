using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace BluetoothManager
{
    public class BluetoothDeviceModel : INotifyPropertyChanged
    {
        private int? _rssi;
        private string _type = "";
        private string _name = "";
        private bool _canConnect = true;
        private bool _isPaired;
        private bool _isFavorite;
        private int? _batteryLevel;
        private DateTimeOffset _lastSeen = DateTimeOffset.Now;
        private double _rssiBarWidth;
        private SolidColorBrush? _distanceBrush;

        public string ConnectButtonText { get; set; } = "Connect";
        public string Id { get; set; } = "";
        public string BluetoothType { get; set; } = "";
        public string MacAddress { get; set; } = "";
        public ulong RawBluetoothAddress { get; set; }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        public bool IsPaired
        {
            get => _isPaired;
            set
            {
                _isPaired = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PairButtonText));
                OnPropertyChanged(nameof(PairButtonVisibility));
            }
        }

        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                _isFavorite = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FavoriteGlyph));
            }
        }

        public string FavoriteGlyph => IsFavorite ? "\uE735" : "\uE734";

        public string PairButtonText => IsPaired
            ? ResHelper.Get("BtnUnpair", "Unpair")
            : ResHelper.Get("BtnPair", "Pair");

        public Visibility PairButtonVisibility =>
            string.IsNullOrEmpty(Id) || Id.StartsWith("ble-adv:", StringComparison.OrdinalIgnoreCase)
                ? Visibility.Collapsed
                : Visibility.Visible;

        public int? Rssi
        {
            get => _rssi;
            set
            {
                _rssi = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RssiString));
                OnPropertyChanged(nameof(DistanceLabel));
                OnPropertyChanged(nameof(ApproximateDistance));
                OnPropertyChanged(nameof(DistanceSummary));
                UpdateDistanceVisuals();
            }
        }

        public string RssiString
        {
            get
            {
                if (!Rssi.HasValue) return "──";
                return $"{Rssi} dBm";
            }
        }

        /// <summary>Human distance band: Very Close / Within Range / Far.</summary>
        public string DistanceLabel
        {
            get
            {
                if (!Rssi.HasValue) return "";
                if (Rssi >= -55)
                    return ResHelper.Get("RadarVeryClose", "Very Close");
                if (Rssi >= -70)
                    return ResHelper.Get("RadarInRadius", "Within Range");
                return ResHelper.Get("RadarFarAway", "Far Away");
            }
        }

        /// <summary>Rough free-space distance estimate from RSSI.</summary>
        public string ApproximateDistance
        {
            get
            {
                if (!Rssi.HasValue) return "";
                // Path-loss estimate: d = 10^((Tx - RSSI)/(10*n)), Tx≈-59 dBm, n≈2
                double meters = Math.Pow(10, (-59 - Rssi.Value) / 20.0);
                if (meters < 1) return "<1 m";
                if (meters < 15) return $"~{Math.Round(meters)} m";
                return ">15 m";
            }
        }

        public string DistanceSummary
        {
            get
            {
                if (!Rssi.HasValue) return "──";
                string dist = ApproximateDistance;
                string band = DistanceLabel;
                if (string.IsNullOrEmpty(band)) return $"{dist} · {RssiString}";
                return $"{band} · {dist}";
            }
        }

        public double RssiBarWidth
        {
            get => _rssiBarWidth;
            private set { _rssiBarWidth = value; OnPropertyChanged(); }
        }

        public SolidColorBrush DistanceBrush
        {
            get => _distanceBrush ??= new SolidColorBrush(Color.FromArgb(255, 148, 163, 184));
            private set { _distanceBrush = value; OnPropertyChanged(); }
        }

        private void UpdateDistanceVisuals()
        {
            if (!Rssi.HasValue)
            {
                RssiBarWidth = 0;
                DistanceBrush = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184));
                return;
            }

            double clamped = Math.Clamp(Rssi.Value, -100, -40);
            RssiBarWidth = ((clamped + 100) / 60.0) * 100.0;

            Color color = Rssi.Value switch
            {
                >= -55 => Color.FromArgb(255, 34, 197, 94),   // green — very close
                >= -70 => Color.FromArgb(255, 14, 165, 233),  // sky — near
                >= -85 => Color.FromArgb(255, 245, 158, 11),  // amber — mid
                _ => Color.FromArgb(255, 239, 68, 68)         // red — far
            };
            DistanceBrush = new SolidColorBrush(color);
        }

        public bool CanConnect
        {
            get => _canConnect;
            set
            {
                _canConnect = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConnectButtonVisibility));
            }
        }

        public Visibility ConnectButtonVisibility => CanConnect ? Visibility.Visible : Visibility.Collapsed;

        public int? BatteryLevel
        {
            get => _batteryLevel;
            set
            {
                _batteryLevel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasBattery));
                OnPropertyChanged(nameof(BatteryString));
            }
        }

        public Visibility HasBattery => BatteryLevel.HasValue ? Visibility.Visible : Visibility.Collapsed;
        public string BatteryString => BatteryLevel.HasValue ? $"{BatteryLevel}%" : "";

        public DateTimeOffset LastSeen
        {
            get => _lastSeen;
            set { _lastSeen = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
