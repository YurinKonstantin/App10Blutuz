using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Streams;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace BluetoothManager
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AnalyzerPage : Page
    {
        private readonly ResourceLoader _resourceLoader = new();
        // Коллекции для привязки к спискам UI
        public ObservableCollection<BluetoothDeviceModel> BleDevices { get; set; } = new();
        public ObservableCollection<BleServiceModel> BleServices { get; set; } = new();
        public ObservableCollection<BleCharacteristicModel> BleCharacteristics { get; set; } = new();

        private BluetoothLEDevice _currentBleDevice;
        private System.Threading.CancellationTokenSource _gattCancellationTokenSource;




        private BluetoothLEAdvertisementWatcher _bleAdvWatcher;
    private System.Collections.Generic.HashSet<string> _detectedMacs = new();
    public AnalyzerPage()
        {
            InitializeComponent();
            BleDeviceComboBox.ItemsSource = BleDevices;
            ServicesListView.ItemsSource = BleServices;
            CharacteristicsListView.ItemsSource = BleCharacteristics;
            BleDeviceComboBox.PlaceholderText = _resourceLoader.GetString("BleComboPlaceholder") ?? "Select BLE Device...";

            // ИСПРАВЛЕНИЕ: Локализуем новые вынесенные заголовки колонок
            AnalyzerHeader.Text = _resourceLoader.GetString("AnalyzerHeader") ?? "GATT Services";
            
            CharacteristicsHeader.Text = _resourceLoader.GetString("CharacteristicsHeader") ?? "Characteristics";
            AnalyzerTitle.Text = _resourceLoader.GetString("AnalyzerTitle") ?? "Bluetooth LE Analyzer (PRO)";

            this.Loaded += AnalyzerPage_Loaded;
        }
        // Кнопка поиска BLE устройств вокруг
        private async void ScanBleButton_Click(object sender, RoutedEventArgs e)
        {
            BleDevices.Clear();
            BleServices.Clear();
            BleCharacteristics.Clear();
            _detectedMacs.Clear();

            ScanBleButton.IsEnabled = false;
            AnalyzerProgress.IsActive = true;

            try
            {
                // 1. Создаем низкоуровневый наблюдатель за BLE рекламой
                _bleAdvWatcher = new BluetoothLEAdvertisementWatcher();

                // Настраиваем активный режим сканирования (Windows будет сама запрашивать имена у устройств)
                _bleAdvWatcher.ScanningMode = BluetoothLEScanningMode.Active;

                // 2. Подписываемся на событие получения пакета данных из эфира
                _bleAdvWatcher.Received += BleAdvWatcher_Received;

                // 3. Запускаем сканирование радиоэфира
                _bleAdvWatcher.Start();

                // Позволяем сканеру поработать 4 секунды, чтобы собрать все устройства вокруг
                await Task.Delay(4000);

                // 4. Останавливаем сканирование
                _bleAdvWatcher.Stop();
                _bleAdvWatcher.Received -= BleAdvWatcher_Received;
            }
            catch (Exception ex)
            {
                await ShowMessageDialogAsync("Ошибка BLE сканера", ex.Message);
            }
            finally
            {
                ScanBleButton.IsEnabled = true;
                AnalyzerProgress.IsActive = false;
            }
        }
        // Срабатывает каждый раз, когда в радиоэфире ПК ловит BLE пакет (даже от чужого телефона или розетки)
        private void BleAdvWatcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            byte[] macBytes = System.BitConverter.GetBytes(args.BluetoothAddress);
            string macAddress = string.Join(":", macBytes.Take(6).Reverse().Select(b => b.ToString("X2")));
            string localName = args.Advertisement.LocalName;

            string deviceDisplayName = !string.IsNullOrEmpty(localName)
                ? localName
                : $"{_resourceLoader.GetString("UnknownDeviceLabel") ?? "[Unknown]"} ({macAddress})";

            if (!_detectedMacs.Contains(macAddress))
            {
                _detectedMacs.Add(macAddress);

                DispatcherQueue.TryEnqueue(() =>
                {
                    BleDevices.Add(new BluetoothDeviceModel
                    {
                        Name = deviceDisplayName,
                        BluetoothType = "BLE",
                        MacAddress = macAddress,
                        // Сохраняем чистый ulong-адрес из радиоэфира
                        RawBluetoothAddress = args.BluetoothAddress
                    });
                });
            }
        }
        // Срабатывает, когда пользователь выбирает сервис в левой колонке
        private async void ServicesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Если ничего не выбрано — выходим
            if (ServicesListView.SelectedItem is not BleServiceModel selectedServiceModel) return;

            // 🎯 ЗАЩИТА ОТ КРЭША В ДЕМО-РЕЖИМЕ:
            // Если объект Service равен null, значит мы находимся в режиме генерации скриншотов.
            // Просто выходим из метода, так как демо-данные для правой колонки мы уже заполнили вручную!
            if (selectedServiceModel.Service == null)
            {
                return;
            }
            BleCharacteristics.Clear();

                try
                {
                    // Получаем все характеристики для выбранного GATT-сервиса
                    var characteristicsResult = await selectedServiceModel.Service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);

                    if (characteristicsResult.Status == GattCommunicationStatus.Success)
                    {
                        foreach (var characteristic in characteristicsResult.Characteristics)
                        {
                            // Формируем список прав (Read, Write, Notify и т.д.)
                            string props = characteristic.CharacteristicProperties.ToString();
                          
                            string defaultClickText = _resourceLoader.GetString("CharClickToRead");

                            BleCharacteristics.Add(new BleCharacteristicModel
                            {
                                Name = DisplayHelpers.ResolveCharacteristicName(characteristic.Uuid),
                                Uuid = characteristic.Uuid.ToString(),
                                Properties = props,
                                Value = defaultClickText, // Базовое состояние
                                Characteristic = characteristic
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    string errTitle = _resourceLoader.GetString("ErrorGattTitle");
                    await ShowMessageDialogAsync(errTitle, ex.Message);
                }
            
        }


        private async void AnalyzerPage_Loaded(object sender, RoutedEventArgs e)
        {
            
            this.Loaded -= AnalyzerPage_Loaded;

            // Проверяем лицензию в магазине Microsoft
            await LicenseManager.CheckLicenseAsync();
            UpdateUiState();
       
        }

        private void UpdateUiState()
        {
            // Переключаем видимость панелей на основе флага лицензии
            if (LicenseManager.IsProUser)
            {
                PurchasePanel.Visibility = Visibility.Collapsed;
                AnalyzerPanel.Visibility = Visibility.Visible;
            }
            else
            {
                PurchasePanel.Visibility = Visibility.Visible;
                AnalyzerPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async void UnlockProButton_Click(object sender, RoutedEventArgs e)
        {
            UnlockProButton.IsEnabled = false;

            // Вызываем системное окно оплаты Microsoft Store
            bool success = await LicenseManager.RequestPurchaseAsync();

            if (success)
            {
                string title = _resourceLoader.GetString("ProPurchaseSuccessTitle");
                string content = _resourceLoader.GetString("ProPurchaseSuccessContent");
                await ShowMessageDialogAsync(title, content);
                UpdateUiState(); // Разблокируем интерфейс
            }
            else
            {
                string title = _resourceLoader.GetString("ProPurchaseFailTitle");
                string content = _resourceLoader.GetString("ProPurchaseFailContent");
                await ShowMessageDialogAsync(title, content);
                UnlockProButton.IsEnabled = true;
            }
        }

        private async System.Threading.Tasks.Task ShowMessageDialogAsync(string title, string content)
        {
            if (App.MainWindow?.Content?.XamlRoot == null) return;

            ContentDialog dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "ОК",
                XamlRoot = App.MainWindow.Content.XamlRoot
            };

            await dialog.ShowAsync();
        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            // Если пользователь уходит с этой страницы, отменяем все фоновые сетевые операции GATT
            if (_gattCancellationTokenSource != null)
            {
                _gattCancellationTokenSource.Cancel();
                _gattCancellationTokenSource.Dispose();
                _gattCancellationTokenSource = null;
            }
            // 🎯 ДОБАВЛЕНО: Отменяем фоновые операции чтения характеристик
            if (_charCancellationTokenSource != null)
            {
                _charCancellationTokenSource.Cancel();
                _charCancellationTokenSource.Dispose();
                _charCancellationTokenSource = null;
            }

            _currentBleDevice?.Dispose();
            _currentBleDevice = null;

            base.OnNavigatedFrom(e);
        }
        private async void BleDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            // Если ничего не выбрано — выходим
            if (BleDeviceComboBox.SelectedItem is not BluetoothDeviceModel selectedDevice) return;

            // 🎯 ЗАЩИТА ОТ СОБЫТИЯ ПОДКЛЮЧЕНИЯ В ДЕМО-РЕЖИМЕ:
            // У наших демонстрационных устройств адрес равен 0. Если это так, мы просто выходим
            // из метода подключения, так как все данные для скриншота мы уже заполнили вручную!
            if (selectedDevice.RawBluetoothAddress == 0)
            {
                return;
            }

            // 1. ОТМЕНА СТАРОЙ ОПЕРАЦИИ: Если прошлый запрос еще выполняется, принудительно гасим его
            if (_gattCancellationTokenSource != null)
            {
                _gattCancellationTokenSource.Cancel();
                _gattCancellationTokenSource.Dispose();
                _gattCancellationTokenSource = null;
            }
            BleServices.Clear();
            BleCharacteristics.Clear();

            //if (BleDeviceComboBox.SelectedItem is BluetoothDeviceModel selectedDevice)
            {
                // 2. БЛОКИРОВКА UI: Выключаем ComboBox и кнопку, чтобы пользователь не спамил кликами
                BleDeviceComboBox.IsEnabled = false;
                ScanBleButton.IsEnabled = false;
                AnalyzerProgress.IsActive = true;

                // Создаем новый токен отмены для текущей операции подключения
                _gattCancellationTokenSource = new CancellationTokenSource();
                var token = _gattCancellationTokenSource.Token;
                
                try
                {
                    // Проверяем отмену перед началом тяжелой операции
                    token.ThrowIfCancellationRequested();
                    // ИСПРАВЛЕНИЕ: Подключаемся напрямую по радио-адресу, без сбойных текстовых ID!
                    _currentBleDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(selectedDevice.RawBluetoothAddress);

                    if (_currentBleDevice == null)
                    {
                        string errTitle = _resourceLoader.GetString("ErrorConnBleTitle");
                        string errMsg = _resourceLoader.GetString("ErrorNoBleDevice");
                        await ShowMessageDialogAsync(errTitle, errMsg);
                        return;
                    }
                    // Проверяем отмену после создания объекта устройства
                    token.ThrowIfCancellationRequested();
                    // Запрашиваем структуру GATT-сервисов устройства
                    var servicesResult = await _currentBleDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                    // Проверяем отмену после получения ответа от Bluetooth-стека
                    token.ThrowIfCancellationRequested();
                    if (servicesResult.Status == GattCommunicationStatus.Success)
                    {
                        foreach (var service in servicesResult.Services)
                        {// Еще одна проверка внутри цикла для идеальной отзывчивости интерфейса
                            token.ThrowIfCancellationRequested();
                            BleServices.Add(new BleServiceModel
                            {
                                Name = DisplayHelpers.ResolveServiceName(service.Uuid),
                                Uuid = service.Uuid.ToString(),
                                Service = service
                            });
                        }
                    }
                    else
                    {
                        string errTitle = _resourceLoader.GetString("ErrorGattTitle");
                        await ShowMessageDialogAsync(errTitle, $"Status: {servicesResult.Status}");

                    }
                }
                catch (OperationCanceledException)
                {
                    // Метод был успешно отменен пользователем, так как он выбрал другое устройство.
                    // Просто игнорируем это исключение, не ломая лог и интерфейс.
                }
                catch (Exception ex)
                {
                    string errTitle = _resourceLoader.GetString("ErrorConnBleTitle");
                    await ShowMessageDialogAsync(errTitle, ex.Message);
                }
                finally
                {
                    // 3. РАЗБЛОКИРОВКА UI: Возвращаем элементы управления в исходное состояние
                    BleDeviceComboBox.IsEnabled = true;
                    ScanBleButton.IsEnabled = true;
                    AnalyzerProgress.IsActive = false;
                  
                }
            }
        }

        private System.Threading.CancellationTokenSource _charCancellationTokenSource;


        private async void CharacteristicsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
            // Если ничего не выбрано — выходим
            if (CharacteristicsListView.SelectedItem is not BleCharacteristicModel selectedCharModel) return;

            // 1. ОТМЕНА СТАРОЙ ОПЕРАЦИИ ЧТЕНИЯ: Если прошлый запрос еще выполняется, принудительно гасим его
            if (_charCancellationTokenSource != null)
            {
                _charCancellationTokenSource.Cancel();
                _charCancellationTokenSource.Dispose();
                _charCancellationTokenSource = null;
            }
            //if (CharacteristicsListView.SelectedItem is BleCharacteristicModel selectedCharModel)
            {
            var characteristic = selectedCharModel.Characteristic;
                // 🎯 ЗАЩИТА ОТ КРЭША В ДЕМО-РЕЖИМЕ:
                // Если объект характеристики равен null, значит мы находимся в режиме генерации скриншотов.
                // Просто выходим, так как демо-данные уже заполнены вручную!
                if (characteristic == null)
                {
                    return;
                }
                // Проверяем, поддерживает ли характеристика чтение (Read)
                if (!characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read))
            {
                    selectedCharModel.Value = _resourceLoader.GetString("CharWriteOnlyError");

                    return;
            }
                // 2. БЛОКИРОВКА UI СПИСКА: Выключаем список характеристик на время чтения данных из эфира
                CharacteristicsListView.IsEnabled = false;
                AnalyzerProgress.IsActive = true;
                // Создаем новый токен отмены для текущей операции чтения
                _charCancellationTokenSource = new CancellationTokenSource();
                var token = _charCancellationTokenSource.Token;
                try
            {
                    // Проверяем отмену перед началом асинхронного радио-запроса
                    token.ThrowIfCancellationRequested();


                    // 1. Считываем значение характеристики из BLE-устройства
                    GattReadResult result = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
                    // Проверяем отмену сразу после завершения сетевого запроса
                    token.ThrowIfCancellationRequested();
                    if (result.Status == GattCommunicationStatus.Success)
                {
                    IBuffer buffer = result.Value;

                    if (buffer.Length > 0)
                    {
                        // 2. Читаем сырые байты
                        byte[] data = new byte[buffer.Length];
                        using (var reader = DataReader.FromBuffer(buffer))
                        {
                            reader.ReadBytes(data);
                        }

                        // 3. Конвертируем байты в понятные форматы
                        // Формат А: Сырой HEX (например: "48 45 4C 4C 4F")
                        string hexString = string.Join(" ", data.Select(b => b.ToString("X2")));

                        // Формат Б: Попытка прочесть как текст (ASCII/UTF-8)
                        string textString = "";
                        try
                        {
                            textString = System.Text.Encoding.UTF8.GetString(data).Trim();
                            // Если в строке есть нечитаемые управляющие символы, оставляем только HEX
                            if (textString.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t'))
                            {
                                textString = "";
                            }
                        }
                        catch
                        {
                            textString = "";
                        }
                            // Еще одна проверка отмены перед записью в UI модель
                            token.ThrowIfCancellationRequested();

                            // Выводим результат: если есть текст, пишем "Текст (HEX)", иначе просто HEX
                            if (!string.IsNullOrEmpty(textString))
                        {
                            selectedCharModel.Value = $"{textString} [HEX: {hexString}]";
                        }
                        else
                        {
                            // Специфика для стандартной батареи (Battery Level): она передает 1 байт со значением от 0 до 100
                            if (characteristic.Uuid.ToString().Contains("2a19") && data.Length == 1)
                            {
                                selectedCharModel.Value = $"{data[0]}%";
                            }
                            else
                            {
                                selectedCharModel.Value = $"HEX: {hexString}";
                            }
                        }
                    }
                    else
                    {
                            selectedCharModel.Value = _resourceLoader.GetString("CharEmptyBuffer");
                        }
                }
                else
                {
                        string baseError = _resourceLoader.GetString("CharReadError");
                        selectedCharModel.Value = $"{baseError} {result.Status}";
                    }
            }
                catch (OperationCanceledException)
                {
                    // Поток чтения был успешно прерван, так как пользователь переключился на другую характеристику.
                    // Тихо выходим без вывода ошибок на экран.
                }
                catch (Exception ex)
            {
                    selectedCharModel.Value = $"Error: {ex.Message}";
                }
            finally
            {
                    // 3. РАЗБЛОКИРОВКА UI: Возвращаем список в активное состояние
                    CharacteristicsListView.IsEnabled = true;
                    AnalyzerProgress.IsActive = false;
            }
        }
    }
        public void LoadDemoDataForAnalyzerScreenshot()
        {
            BleDevices.Clear();
            BleServices.Clear();
            BleCharacteristics.Clear();

            // 📸 СКРИНШОТ 4: Низкоуровневый GATT Анализатор BLE в действии
            // Наполняем выпадающий список профессиональными BLE-устройствами вокруг
            BleDevices.Add(new BluetoothDeviceModel { Name = "Medical-MultiSensor-Pro", BluetoothType = "BLE", MacAddress = "D0:B5:C2:33:44:EF" });
            BleDevices.Add(new BluetoothDeviceModel { Name = "ClimateControl-Beacon", BluetoothType = "BLE", MacAddress = "C4:7F:51:88:AA:BB" });
            BleDevices.Add(new BluetoothDeviceModel { Name = "Smart-Lock-Gateway", BluetoothType = "BLE", MacAddress = "00:A0:50:BC:11:22" });
            BleDeviceComboBox.SelectedIndex = 0;

            // Заполняем ЛЕВУЮ КОЛОНКУ (GATT Сервисы)
            BleServices.Add(new BleServiceModel { Name = "Heart Rate Service (Пульсометр)", Uuid = "0000180D-0000-1000-8000-00805F9B34FB" });
            BleServices.Add(new BleServiceModel { Name = "Health Thermometer (Термометр)", Uuid = "00001809-0000-1000-8000-00805F9B34FB" });
            BleServices.Add(new BleServiceModel { Name = "Battery Service (Уровень батареи)", Uuid = "0000180F-0000-1000-8000-00805F9B34FB" });
            BleServices.Add(new BleServiceModel { Name = "Device Information (Данные устройства)", Uuid = "0000180A-0000-1000-8000-00805F9B34FB" });
            BleServices.Add(new BleServiceModel { Name = "Custom Payload Profile (Заводской протокол)", Uuid = "6E400001-B5A3-F393-E0A9-E50E24DCCA9E" });
            ServicesListView.SelectedIndex = 0;

            // Заполняем ПРАВУЮ КОЛОНКУ (Характеристики выбранного пульсометра)
            // Показываем разные права доступа (Read, Write, Notify, Indicate) и прочитанные живые данные
            BleCharacteristics.Add(new BleCharacteristicModel { Name = "Heart Rate Measurement", Uuid = "00002A37-0000-1000-8000-00805F9B34FB", Properties = "Notify", Value = "74 bpm [HEX: 00 4A]" });
            BleCharacteristics.Add(new BleCharacteristicModel { Name = "Body Sensor Location", Uuid = "00002A38-0000-1000-8000-00805F9B34FB", Properties = "Read", Value = "Chest / Wrist [HEX: 01]" });
            BleCharacteristics.Add(new BleCharacteristicModel { Name = "Heart Rate Control Point", Uuid = "00002A39-0000-1000-8000-00805F9B34FB", Properties = "Write", Value = "0x00 (Write Only)" });
        }

    }
}

