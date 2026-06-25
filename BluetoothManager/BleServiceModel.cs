using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BluetoothManager
{
    // Модель для GATT-Сервиса (Левая колонка)
    public class BleServiceModel
    {
        public string Name { get; set; }        // Имя сервиса (например, Device Information)
        public string Uuid { get; set; }        // Полный GUID / UUID сервиса
        public GattDeviceService Service { get; set; } // Ссылка на сам системный объект
        public string DisplayText => $"{Name}\n{Uuid}";
    }

    // Модель для GATT-Характеристики (Правая колонка)
    public class BleCharacteristicModel : INotifyPropertyChanged
    {
        private string _value;
        public string Name { get; set; }        // Имя характеристики (например, Battery Level)
        public string Uuid { get; set; }        // UUID характеристики
        public string Properties { get; set; }  // Права доступа (Read, Write, Notify)
                                                // Добавляем полноценное свойство с уведомлением интерфейса
        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged(); // Мгновенно обновляет текст в XAML карточке!
                }
            }
        }
        public GattCharacteristic Characteristic { get; set; } // Ссылка на объект системы
                                                               // Реализация интерфейса для динамического обновления UI
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

    }
}
