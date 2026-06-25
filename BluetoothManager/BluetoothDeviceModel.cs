using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BluetoothManager
{
    public class BluetoothDeviceModel
    {
        private int? _rssi;
        private string _type;
        private string _name;
        // Локализованный текст для кнопки подключения этой карточки
        public string ConnectButtonText { get; set; }
        public string Id { get; set; }

        // 1. Добавляем тип технологии: Classic или BLE
        public string BluetoothType { get; set; }

        // 2. Добавляем MAC-адрес устройства
        public string MacAddress { get; set; }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        // Текстовый статус (Сопряжено / Доступно)
        public string Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }
        // Числовой адрес устройства для прямого подключения (основа PRO-анализатора)
        public ulong RawBluetoothAddress { get; set; }

        // 3. Добавляем уровень сигнала RSSI — основа для нашего Радара
        public int? Rssi
        {
            get => _rssi;
            set
            {
                _rssi = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RssiString));
                OnPropertyChanged(nameof(RadarIndicator));
            }
        }

        // Форматированный вывод сигнала
        public string RssiString => Rssi.HasValue ? $"{Rssi} dBm" : "──";

        // Визуальный текстовый индикатор радара "Тепло / Холодно"
        public string RadarIndicator
        {
            get
            {
                if (!Rssi.HasValue) return "❔";

                // Используем глобальный локализатор App.ResLoader
                if (Rssi >= -60)
                    return App.ResLoader.GetString("RadarVeryClose") ?? "🔥 Very Close";

                if (Rssi >= -80)
                    return App.ResLoader.GetString("RadarInRadius") ?? "⚡ Within Range";

                return App.ResLoader.GetString("RadarFarAway") ?? "❄️ Far Away";
            }
        }

        public int? BatteryLevel { get; set; }
        public Visibility HasBattery => BatteryLevel.HasValue ? Visibility.Visible : Visibility.Collapsed;

        // Реализация интерфейса для динамического обновления UI
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

