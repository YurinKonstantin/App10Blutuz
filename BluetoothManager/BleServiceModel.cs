using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BluetoothManager
{
    public class BleServiceModel
    {
        public string Name { get; set; } = "";
        public string Uuid { get; set; } = "";
        public GattDeviceService? Service { get; set; }
        public ObservableCollection<BleCharacteristicModel> Characteristics { get; } = new();
        public string DisplayText => $"{Name}\n{Uuid}";
        public override string ToString() => Name;
    }

    public class BleCharacteristicModel : INotifyPropertyChanged
    {
        private string _value = "";
        private bool _isSubscribed;

        public string Name { get; set; } = "";
        public string Uuid { get; set; } = "";
        public string Properties { get; set; } = "";
        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSubscribed
        {
            get => _isSubscribed;
            set
            {
                _isSubscribed = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SubscribeButtonText));
            }
        }

        public string SubscribeButtonText => IsSubscribed
            ? ResHelper.Get("BtnUnsubscribe", "Unsubscribe")
            : ResHelper.Get("BtnSubscribe.Content", "Subscribe");

        public bool CanRead { get; set; }
        public bool CanWrite { get; set; }
        public bool CanNotify { get; set; }

        public GattCharacteristic? Characteristic { get; set; }
        public ObservableCollection<BleDescriptorModel> Descriptors { get; } = new();

        public override string ToString() => Name;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class BleDescriptorModel : INotifyPropertyChanged
    {
        private string _value = "";

        public string Name { get; set; } = "";
        public string Uuid { get; set; } = "";
        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }
        public GattDescriptor? Descriptor { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
