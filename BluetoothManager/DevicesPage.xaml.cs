using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace BluetoothManager
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class DevicesPage : Page
    {// Глобальный кэш для хранения ВСЕХ найденных устройств в эфире
        private readonly System.Collections.Generic.List<BluetoothDeviceModel> _allDiscoveredDevices = new();

        public ObservableCollection<BluetoothDeviceModel> Devices { get; set; } = new();
        private readonly ResourceLoader _resourceLoader = new();

        private BluetoothAdapter _bluetoothAdapter;
        private DeviceWatcher _deviceWatcher;

        // Поля клиента
        private BluetoothDevice _connectedDevice;
        private RfcommDeviceService _rfcommService;
        private DataWriter _dataWriter;
        private DataReader _dataReader;
        private bool _isListening = false;

        // Поля сервера
        private RfcommServiceProvider _rfcommProvider;
        private StreamSocketListener _socketListener;
        private StreamSocket _serverClientSocket;
        private DataWriter _serverDataWriter;
        private DataReader _serverDataReader;
        private bool _isServerRunning = false;
       
        public DevicesPage()
        {
            this.InitializeComponent();
            DeviceListView.ItemsSource = Devices;
            this.Loaded += DevicesPage_Loaded;
        }

        private async void DevicesPage_Loaded(object sender, RoutedEventArgs e)
        {
            
            this.Loaded -= DevicesPage_Loaded;
            await CheckBluetoothAvailabilityAsync();
           
        }

        // Вставьте сюда методы:
        private async Task<bool> CheckBluetoothAvailabilityAsync()
        {
            try
            {
                // Получаем радиомодуль по умолчанию
                _bluetoothAdapter = await BluetoothAdapter.GetDefaultAsync();

                if (_bluetoothAdapter == null)
                {
                    ScanButton.IsEnabled = false;

                    string title = _resourceLoader.GetString("MsgBtErrorTitle");
                    string content = _resourceLoader.GetString("MsgBtErrorContent");

                    await ShowMessageDialogAsync(title, content);
                    return false;
                }

                if (!_bluetoothAdapter.IsCentralRoleSupported && !_bluetoothAdapter.IsPeripheralRoleSupported)
                {
                    ScanButton.IsEnabled = false;

                    string title = _resourceLoader.GetString("MsgCompatErrorTitle");
                    string content = _resourceLoader.GetString("MsgCompatErrorContent");

                    await ShowMessageDialogAsync(title, content);
                    return false;
                }

                // Проверяем, включен ли тумблер Bluetooth в Windows
                var radio = await _bluetoothAdapter.GetRadioAsync();
                if (radio == null || radio.State != Windows.Devices.Radios.RadioState.On)
                {
                    ScanButton.IsEnabled = false;

                    string title = _resourceLoader.GetString("MsgBtOffTitle");
                    string content = _resourceLoader.GetString("MsgBtOffContent");

                    await ShowMessageDialogAsync(title, content);
                    return false;
                }

                // Все проверки пройдены успешно
                ScanButton.IsEnabled = true;
                return true;
            }
            catch (Exception ex)
            {
                ScanButton.IsEnabled = false;

                string title = _resourceLoader.GetString("MsgInitErrorTitle");
                string baseContent = _resourceLoader.GetString("MsgInitErrorContent");

                await ShowMessageDialogAsync(title, $"{baseContent} {ex.Message}");
                return false;
            }
        }
        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Проверяем доступность Bluetooth
            if (!await CheckBluetoothAvailabilityAsync())
            {
                return;
            }

            // 2. Если сканирование уже идет — останавливаем его
            if (_deviceWatcher != null && (_deviceWatcher.Status == DeviceWatcherStatus.Started || _deviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted))
            {
                StopScanning();
                return;
            }

            // Очищаем список перед новым поиском
            Devices.Clear();
            _allDiscoveredDevices.Clear();
            ScanProgress.IsActive = true;

            // Меняем текст кнопки на "Остановить" (заберите из ресурсов или оставьте локализованную строку)
            ScanButton.Content = _resourceLoader.GetString("ScanButtonStop") ?? "Stop";

            // 3. Создаем универсальный AQS-селектор для Windows 11 Desktop (Классический + BLE)
            // Этот фильтр гарантированно работает в WinUI 3 для AssociationEndpoint
            string aqsFilter = "System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\" OR System.Devices.Aep.ProtocolId:=\"{bb7bb05e-cc72-411b-a17b-738b10a971d5}\"";

            string[] requestedProperties = new string[]
            {
        "System.Devices.Aep.IsConnected",
        "System.Devices.Aep.IsPaired",
        "System.Devices.Aep.SignalStrength"
            };

            // Создаем watcher для динамического отслеживания устройств вокруг
            _deviceWatcher = DeviceInformation.CreateWatcher(
                aqsFilter,
                requestedProperties,
                DeviceInformationKind.AssociationEndpoint);

            // 4. Подписываемся на события watcher'а
            _deviceWatcher.Added += DeviceWatcher_Added;
            _deviceWatcher.Updated += DeviceWatcher_Updated;
            _deviceWatcher.Removed += DeviceWatcher_Removed;
            _deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            _deviceWatcher.Stopped += DeviceWatcher_Stopped;

            // Start!
            _deviceWatcher.Start();
        }
        private void StopScanning()
        {
            if (_deviceWatcher != null)
            {
                _deviceWatcher.Stop();
            }
        }
        // - Все методы DeviceWatcher_...
        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string deviceId)
            {
                button.IsEnabled = false;
                StatusTextBlock.Text = _resourceLoader.GetString("StatusConnecting");

                try
                {
                    // 1. Получаем объект Bluetooth-устройства
                    _connectedDevice = await BluetoothDevice.FromIdAsync(deviceId);

                    if (_connectedDevice == null)
                    {
                        throw new Exception(_resourceLoader.GetString("ErrorDeviceAccess"));
                    }

                    // 2. Запрашиваем RFCOMM службы (SPP профиль для COM-порта)
                    var rfcommServices = await _connectedDevice.GetRfcommServicesAsync(BluetoothCacheMode.Uncached);

                    if (rfcommServices.Services.Count == 0)
                    {
                        throw new Exception(_resourceLoader.GetString("ErrorNoSppProfile"));
                    }

                    _rfcommService = rfcommServices.Services[0];

                    // 3. Создаем сокет и асинхронно подключаемся
                    Windows.Networking.Sockets.StreamSocket socket = new Windows.Networking.Sockets.StreamSocket();
                    await socket.ConnectAsync(_rfcommService.ConnectionHostName, _rfcommService.ConnectionServiceName);

                    // 4. Настраиваем потоки ввода/вывода
                    _dataWriter = new DataWriter(socket.OutputStream);
                    _dataReader = new DataReader(socket.InputStream);
                    _dataReader.InputStreamOptions = InputStreamOptions.Partial; // Читаем пакеты по мере их готовности

                    // Выводим локализованный статус успешного подключения
                    string statusConnectedBase = _resourceLoader.GetString("StatusConnectedTo");
                    StatusTextBlock.Text = $"{statusConnectedBase} {_connectedDevice.Name}";

                    InputTextBox.IsEnabled = true;
                    SendButton.IsEnabled = true;

                    // 5. Запускаем безопасный фоновый поток чтения данных
                    if (!_isListening)
                    {
                        _isListening = true;
                        _ = ListenForIncomingDataAsync();
                    }
                }
                catch (Exception ex)
                {
                    StatusTextBlock.Text = _resourceLoader.GetString("StatusConnError");
                    string errorTitle = _resourceLoader.GetString("MsgConnErrorTitle");
                    await ShowMessageDialogAsync(errorTitle, ex.Message);
                    button.IsEnabled = true;
                }
            }
        }
        // Метод отправки данных
        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string message = InputTextBox.Text;
            if (string.IsNullOrWhiteSpace(message) || _dataWriter == null) return;

            try
            {
                // Добавляем перенос строки (стандарт для работы с AT-командами и микроконтроллерами)
                string messageWithLine = message + "\n";

                _dataWriter.WriteString(messageWithLine);
                await _dataWriter.StoreAsync(); // Принудительно отправляем данные в буфер Bluetooth

                string logOutputPrefix = _resourceLoader.GetString("LogOutput");
                TerminalLogTextBox.Text += $"{logOutputPrefix}: {message}\n";
                InputTextBox.Text = string.Empty;
            }
            catch (Exception ex)
            {
                string logErrorPrefix = _resourceLoader.GetString("LogSendError");
                TerminalLogTextBox.Text += $"{logErrorPrefix}: {ex.Message}\n";
            }
        }
        // Отправка по нажатию Enter в поле ввода
        private void InputTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                SendButton_Click(this, new RoutedEventArgs());
            }
        }
        // Асинхронное фоновое чтение входящего потока
        private async System.Threading.Tasks.Task ListenForIncomingDataAsync()
        {
            try
            {
                while (_isListening && _dataReader != null)
                {
                    // Асинхронно ожидаем данные из Bluetooth-потока (буфер до 1024 байт)
                    uint bytesRead = await _dataReader.LoadAsync(1024);

                    if (bytesRead > 0)
                    {
                        string incomingMessage = _dataReader.ReadString(bytesRead);

                        // Перенаправляем вывод в UI-поток WinUI 3
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            string logInputPrefix = _resourceLoader.GetString("LogInput");
                            TerminalLogTextBox.Text += $"{logInputPrefix}: {incomingMessage}";
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // Если устройство отключили или пропало питание — корректно обрабатываем обрыв связи
                DispatcherQueue.TryEnqueue(() =>
                {
                    string logConnLostPrefix = _resourceLoader.GetString("LogConnLost");
                    TerminalLogTextBox.Text += $"{logConnLostPrefix}: {ex.Message}\n";
                    StatusTextBlock.Text = _resourceLoader.GetString("StatusDisconnected");
                    InputTextBox.IsEnabled = false;
                    SendButton.IsEnabled = false;
                });
                _isListening = false;
            }
        }
        private async void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. ИСПРАВЛЕНИЕ: Проверяем коммерческую лицензию пользователя
            if (!LicenseManager.IsProUser)
            {
                // Перед показом окна принудительно обновляем статус лицензии на случай, если покупка уже была сделана
                await LicenseManager.CheckLicenseAsync();

                if (!LicenseManager.IsProUser)
                {
                    // Формируем красивое окно предложения покупки
                    ContentDialog buyServerDialog = new ContentDialog
                    {
                        Title = _resourceLoader.GetString("ServerLockTitle") ?? "PRO Feature",
                        Content = _resourceLoader.GetString("ServerLockContent"),
                        PrimaryButtonText = _resourceLoader.GetString("BtnBuyPro") ?? "Buy PRO",
                        CloseButtonText = _resourceLoader.GetString("Cancel") ?? "Cancel",
                        XamlRoot = App.MainWindow.Content.XamlRoot // Привязка к визуальному дереву WinUI 3
                    };

                    var dialogResult = await buyServerDialog.ShowAsync();

                    // Если пользователь нажал "Купить PRO"
                    if (dialogResult == ContentDialogResult.Primary)
                    {
                        bool purchaseSuccess = await LicenseManager.RequestPurchaseAsync();

                        if (purchaseSuccess)
                        {
                            string successTitle = _resourceLoader.GetString("ProPurchaseSuccessTitle");
                            string successContent = _resourceLoader.GetString("ProPurchaseSuccessContent");
                            await ShowMessageDialogAsync(successTitle, successContent);
                            // Покупка успешна, код идет дальше к запуску сервера!
                        }
                        else
                        {
                            // Пользователь отменил оплату или произошла ошибка сети
                            return;
                        }
                    }
                    else
                    {
                        // Пользователь просто закрыл окно
                        return;
                    }
                }
            }


            // 2. БАЗОВАЯ ЛОГИКА ЗАПУСКА СЕРВЕРА (Выполняется только для PRO пользователей)

            if (!await CheckBluetoothAvailabilityAsync()) return;

            try
            {
                StartServerButton.IsEnabled = false;

                // 1. Создаем провайдер службы RFCOMM со стандартным UUID SerialPort
                _rfcommProvider = await RfcommServiceProvider.CreateAsync(RfcommServiceId.SerialPort);

                // 2. Создаем сокет-слушатель для входящих соединений
                _socketListener = new StreamSocketListener();
                _socketListener.ConnectionReceived += SocketListener_ConnectionReceived;

                // Привязываем слушатель к локальному Bluetooth-имени службы
                await _socketListener.BindServiceNameAsync(
                    _rfcommProvider.ServiceId.AsString(),
                    SocketProtectionLevel.BluetoothEncryptionAllowNullAuthentication);

                // 3. Начинаем трансляцию (Advertising) SDP-записи, чтобы другие устройства увидели наш ПК
                _rfcommProvider.StartAdvertising(_socketListener);

                _isServerRunning = true;
                StopServerButton.IsEnabled = true;
                ServerStatusTextBlock.Text = _resourceLoader.GetString("StatusServerListening");
                TerminalLogTextBox.Text += $"[SERVER]: {_resourceLoader.GetString("StatusServerListening")}\n";
            }
            catch (Exception ex)
            {
                StartServerButton.IsEnabled = true;
                await ShowMessageDialogAsync("Server Error", ex.Message);
            }
        }
        // Срабатывает в фоновом потоке, когда внешнее устройство подключается к нашему серверу на ПК
        private async void SocketListener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            _serverClientSocket = args.Socket;

            // Останавливаем трансляцию, так как клиент уже занял порт (опционально для SPP)
            _rfcommProvider.StopAdvertising();

            _serverDataWriter = new DataWriter(_serverClientSocket.OutputStream);
            _serverDataReader = new DataReader(_serverClientSocket.InputStream);
            _serverDataReader.InputStreamOptions = InputStreamOptions.Partial;

            DispatcherQueue.TryEnqueue(() =>
            {
                ServerStatusTextBlock.Text = _resourceLoader.GetString("StatusServerConnected");
                TerminalLogTextBox.Text += $"[SERVER]: {_resourceLoader.GetString("StatusServerConnected")}\n";

                // Разрешаем отправку данных из ПК обратно подключившемуся клиенту
                InputTextBox.IsEnabled = true;
                SendButton.IsEnabled = true;
            });

            // Запускаем чтение данных от клиента
            try
            {
                while (_isServerRunning && _serverDataReader != null)
                {
                    uint bytesRead = await _serverDataReader.LoadAsync(1024);
                    if (bytesRead > 0)
                    {
                        string incomingMessage = _serverDataReader.ReadString(bytesRead);

                        DispatcherQueue.TryEnqueue(() =>
                        {
                            string logInput = _resourceLoader.GetString("LogInput");
                            TerminalLogTextBox.Text += $"[SERVER {logInput}]: {incomingMessage}";

                            // ЭХО-РЕЖИМ для теста: отправляем эти же данные обратно клиенту автоматичеки
                            _ = EchoServerResponseAsync(incomingMessage);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    TerminalLogTextBox.Text += $"[SERVER]: Connection lost. {ex.Message}\n";
                    ServerStatusTextBlock.Text = _resourceLoader.GetString("StatusDisconnected");
                });
            }
        }
        private async Task EchoServerResponseAsync(string message)
        {
            if (_serverDataWriter == null) return;
            try
            {
                _serverDataWriter.WriteString($"ECHO: {message}");
                await _serverDataWriter.StoreAsync();
            }
            catch { }
        }
        private void StopServerButton_Click(object sender, RoutedEventArgs e)
        {
            _isServerRunning = false;

            if (_rfcommProvider != null)
            {
                try
                {
                    // Безопасно останавливаем вещание. 
                    // Если оно уже было остановлено при подключении клиента, метод просто пропустит этот шаг.
                    _rfcommProvider.StopAdvertising();
                }
                catch (InvalidOperationException)
                {
                    // Игнорируем ошибку, если вещание уже было прекращено системой или кодом подключения
                }
                _rfcommProvider = null;
            }

            if (_socketListener != null)
            {
                _socketListener.Dispose();
                _socketListener = null;
            }

            // Закрываем потоки и сокеты работы с клиентом
            _serverDataWriter?.Dispose();
            _serverDataWriter = null;

            _serverDataReader?.Dispose();
            _serverDataReader = null;

            _serverClientSocket?.Dispose();
            _serverClientSocket = null;

            // Сбрасываем состояние интерфейса
            StartServerButton.IsEnabled = true;
            StopServerButton.IsEnabled = false;
            InputTextBox.IsEnabled = false;
            SendButton.IsEnabled = false;
            ServerStatusTextBlock.Text = _resourceLoader.GetString("StatusServerStopped");

        }
        private async Task ShowMessageDialogAsync(string title, string content)
        {
            // Безопасно берем XamlRoot главного окна приложения, так как страница находится внутри него
            if (App.MainWindow?.Content?.XamlRoot == null) return;

            ContentDialog dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "ОК",
                XamlRoot = App.MainWindow.Content.XamlRoot // Исправлено получение XamlRoot
            };

            await dialog.ShowAsync();
        }
        private async void RootElement_Loaded(object sender, RoutedEventArgs e)
        {
            // Отписываемся, чтобы событие не срабатывало повторно
            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.Loaded -= RootElement_Loaded;
            }

            // Теперь вызов абсолютно безопасен
            await CheckBluetoothAvailabilityAsync();
        }



        private void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate deviceUpdate)
        {
            // Здесь в будущем (в PRO версии) мы будем обновлять уровень сигнала RSSI "на лету"
        }
        private void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate deviceUpdate)
        {
            // Удаляем устройство из списка, если оно пропало из эфира
            DispatcherQueue.TryEnqueue(() =>
            {
                for (int i = 0; i < Devices.Count; i++)
                {
                    if (Devices[i].Id == deviceUpdate.Id)
                    {
                        Devices.RemoveAt(i);
                        break;
                    }
                }
            });
        }

        private void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            // Первый быстрый опрос завершен (но watcher продолжает искать новые устройства)
            DispatcherQueue.TryEnqueue(() =>
            {
                ScanProgress.IsActive = false;
            });
        }

        private void DeviceWatcher_Stopped(DeviceWatcher sender, object args)
        {
            // Срабатывает при полной остановке сканирования
            DispatcherQueue.TryEnqueue(() =>
            {
                ScanProgress.IsActive = false;
                ScanButton.Content = _resourceLoader.GetString("ScanButtonStart") ?? "Scan";
            });
        }
        // Срабатывает, когда найдено новое устройство в эфире

        private void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation device)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // 1. Проверяем, нет ли уже устройства с таким ID в нашем основном кэше
                foreach (var existingDevice in _allDiscoveredDevices)
                {
                    if (existingDevice.Id == device.Id) return;
                }

                bool hasName = !string.IsNullOrEmpty(device.Name);
                string deviceName = hasName ? device.Name : _resourceLoader.GetString("UnknownDeviceLabel");

                // Определяем тип (Classic или BLE)
                string bType = device.Id.Contains("BluetoothLE") ? "BLE" : "Classic";

                // Извлекаем MAC-адрес
                string mac = "";
                if (device.Properties.TryGetValue("System.Devices.Aep.DeviceAddress", out var macObj) && macObj != null)
                {
                    mac = macObj.ToString();
                }

                // Извлекаем RSSI
                int? rssiValue = null;
                if (device.Properties.TryGetValue("System.Devices.Aep.SignalStrength", out var rssiObj) && rssiObj != null)
                {
                    rssiValue = Convert.ToInt32(rssiObj);
                }

                bool isPaired = device.Pairing.IsPaired;
                string deviceStatus = isPaired
                    ? _resourceLoader.GetString("StatusPaired")
                    : _resourceLoader.GetString("StatusAvailable");

                string subTitle = hasName ? deviceStatus : $"{deviceStatus} ({mac})";

                // Создаем новую модель устройства
                var newDevice = new BluetoothDeviceModel
                {
                    Name = deviceName,
                    Id = device.Id,
                    BluetoothType = bType,
                    MacAddress = mac,
                    Type = subTitle,
                    Rssi = rssiValue,
                     ConnectButtonText = _resourceLoader.GetString("BtnConnect") ?? "Connect"
                };

                // 2. Всегда сохраняем устройство в наш скрытый кэш
                _allDiscoveredDevices.Add(newDevice);

                // 3. На экран выводим только если оно с именем ИЛИ если включен тумблер
                if (hasName || ShowUnknownToggle.IsOn)
                {
                    Devices.Add(newDevice);
                }
            });
        }

        private void ShowUnknownToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                string unknownLabel = _resourceLoader.GetString("UnknownDeviceLabel");

                if (toggle.IsOn)
                {
                    // Если включили: добавляем на экран из кэша те устройства, которых там еще нет
                    foreach (var device in _allDiscoveredDevices)
                    {
                        if (device.Name == unknownLabel && !Devices.Contains(device))
                        {
                            Devices.Add(device);
                        }
                    }
                }
                else
                {
                    // Если выключили: мгновенно убираем с экрана все безымянные устройства
                    for (int i = Devices.Count - 1; i >= 0; i--)
                    {
                        if (Devices[i].Name == unknownLabel)
                        {
                            Devices.RemoveAt(i);
                        }
                    }
                }
            }
        }

        public void LoadDemoDataForScreenshots(int step)
        {
            Devices.Clear();
            _allDiscoveredDevices.Clear();

            string btnText = _resourceLoader.GetString("BtnConnect") ?? "Connect";

            if (step == 1)
            {
                // 📸 СКРИНШОТ 1: Лавина устройств вокруг и активный Радар
                StatusTextBlock.Text = _resourceLoader.GetString("StatusDisconnected");
                InputTextBox.IsEnabled = false;
                SendButton.IsEnabled = false;
                TerminalLogTextBox.Text = string.Empty;
                ServerStatusTextBlock.Text = string.Empty;

                Devices.Add(new BluetoothDeviceModel { Name = "ESP32-Weather-Node", BluetoothType = "BLE", MacAddress = "34:94:70:AA:BB:CC", Type = _resourceLoader.GetString("StatusAvailable"), Rssi = -41, ConnectButtonText = btnText });
                Devices.Add(new BluetoothDeviceModel { Name = "Arduino-Robot-Arm", BluetoothType = "Classic", MacAddress = "00:14:03:05:5A:11", Type = _resourceLoader.GetString("StatusPaired"), Rssi = -55, ConnectButtonText = btnText });
                Devices.Add(new BluetoothDeviceModel { Name = "PulseOximeter-Pro", BluetoothType = "BLE", MacAddress = "E7:2C:48:91:04:7F", Type = _resourceLoader.GetString("StatusAvailable"), Rssi = -68, ConnectButtonText = btnText });
                Devices.Add(new BluetoothDeviceModel { Name = "STM32-CNC-Controller", BluetoothType = "Classic", MacAddress = "00:80:E1:14:BC:99", Type = _resourceLoader.GetString("StatusPaired"), Rssi = -74, ConnectButtonText = btnText });
                Devices.Add(new BluetoothDeviceModel { Name = "Smart-Glow-Lamp", BluetoothType = "BLE", MacAddress = "A4:C1:38:D2:11:55", Type = _resourceLoader.GetString("StatusAvailable"), Rssi = -82, ConnectButtonText = btnText });
                Devices.Add(new BluetoothDeviceModel { Name = _resourceLoader.GetString("UnknownDeviceLabel"), BluetoothType = "BLE", MacAddress = "7C:2A:DB:88:43:21", Type = _resourceLoader.GetString("StatusAvailable"), Rssi = -89, ConnectButtonText = btnText });
                Devices.Add(new BluetoothDeviceModel { Name = "Beacon-Navigator-X", BluetoothType = "BLE", MacAddress = "FF:EE:DD:CC:BB:AA", Type = _resourceLoader.GetString("StatusAvailable"), Rssi = -95, ConnectButtonText = btnText });
            }
            else if (step == 2)
            {
                // 📸 СКРИНШОТ 2: Интенсивный двунаправленный обмен данными (Терминал)
                // Имитируем, что мы подключены к Робо-руке и управляем сервоприводами
                StatusTextBlock.Text = $"{_resourceLoader.GetString("StatusConnectedTo")} Arduino-Robot-Arm";
                InputTextBox.IsEnabled = true;
                SendButton.IsEnabled = true;
                ServerStatusTextBlock.Text = string.Empty;

                // Наполняем левый список сопряженными устройствами, чтобы экран не пустовал
                Devices.Add(new BluetoothDeviceModel { Name = "Arduino-Robot-Arm", BluetoothType = "Classic", MacAddress = "00:14:03:05:5A:11", Type = _resourceLoader.GetString("StatusPaired"), Rssi = -48, ConnectButtonText = btnText });
                Devices.Add(new BluetoothDeviceModel { Name = "STM32-CNC-Controller", BluetoothType = "Classic", MacAddress = "00:80:E1:14:BC:99", Type = _resourceLoader.GetString("StatusPaired"), Rssi = -72, ConnectButtonText = btnText });

                var logOutput = _resourceLoader.GetString("LogOutput") ?? "[OUTPUT]";
                var logInput = _resourceLoader.GetString("LogInput") ?? "[INPUT]";

                TerminalLogTextBox.Text =
                    $"{logOutput}: INIT_SERIAL_SPP\n" +
                    $"{logInput}: AT_BPS_115200_OK\n" +
                    $"{logOutput}: SET_MOTOR_X=145\n" +
                    $"{logInput}: MOTOR_X_ACK_POSITION=145\n" +
                    $"{logOutput}: GET_ANALOG_SENSORS\n" +
                    $"{logInput}: A0=1024;A1=512;A2=0;PRESSURE_METER=84%\n" +
                    $"{logOutput}: SYSTEM_STATUS_CHECK\n" +
                    $"{logInput}: STATUS=OPERATIONAL;VOLTAGE=4.98V;TEMP=32.4C\n" +
                    $"{logOutput}: SET_MOTOR_Y=90\n" +
                    $"{logInput}: MOTOR_Y_ACK_POSITION=90\n" +
                    $"{logOutput}: STREAM_TELEMETRY_START\n" +
                    $"{logInput}: [RAW_DATA] 0xFF 0xAA 0x01 0x23 0x45 0x00 0x1A\n";
            }
            else if (step == 3)
            {
                // 📸 СКРИНШОТ 3: Мощный локальный Bluetooth-Сервер (PRO)
                // Имитируем, что наш ПК развернул сервер и к нему успешно прицепилась внешняя плата
                StatusTextBlock.Text = _resourceLoader.GetString("StatusDisconnected");
                ServerStatusTextBlock.Text = _resourceLoader.GetString("StatusServerConnected") ?? "Server: Client Connected!";
                InputTextBox.IsEnabled = true;
                SendButton.IsEnabled = true;

                Devices.Add(new BluetoothDeviceModel { Name = "Arduino-Robot-Arm", BluetoothType = "Classic", MacAddress = "00:14:03:05:5A:11", Type = _resourceLoader.GetString("StatusPaired"), Rssi = -50, ConnectButtonText = btnText });

                var logInput = _resourceLoader.GetString("LogInput") ?? "[INPUT]";

                TerminalLogTextBox.Text =
                    $"[SERVER]: {_resourceLoader.GetString("StatusServerListening")}\n" +
                    $"[SERVER]: {_resourceLoader.GetString("StatusServerConnected")}\n" +
                    $"[SERVER {logInput}]: CLIENT_HELLO_IDENTITY: REMOTE_CONTROLLER_v2\n" +
                    $"[SERVER {logInput}]: REQ_FIRMWARE_VERSION\n" +
                    $"[SERVER]: ECHO RESPONSE SENT -> BlueIDE_v1.0.0_WinUI3\n" +
                    $"[SERVER {logInput}]: MAVLINK_PACKET_RECEIVED: ID=42;LEN=16\n" +
                    $"[SERVER {logInput}]: PING_HEARTBEAT\n" +
                    $"[SERVER]: ECHO RESPONSE SENT -> PONG_HEARTBEAT_ACK\n";
            }
        }
    }
}
