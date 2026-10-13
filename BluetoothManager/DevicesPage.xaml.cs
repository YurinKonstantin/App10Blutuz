using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BluetoothManager.Demo_Mode;
using BluetoothManager.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;

namespace BluetoothManager
{
    public sealed partial class DevicesPage : Page
    {
        private readonly List<BluetoothDeviceModel> _allDiscoveredDevices = new();
        private readonly HashSet<string> _favorites = FavoritesStore.Load();
        private readonly ResourceLoader _res = new();
        private readonly DeviceWatcherService _watcher = new();
        private readonly RfcommTerminalSession _session = new();
        private readonly RfcommServerSession _server = new();
        private readonly TrafficLogStore _log = new();
        private readonly ObservableCollection<PresetChip> _presetChips = new();
        private readonly ObservableCollection<string> _historyItems = new();
        private readonly ObservableCollection<string> _serviceItems = new();

        public ObservableCollection<BluetoothDeviceModel> Devices { get; } = new();

        private BluetoothAdapter? _bluetoothAdapter;
        private string? _pendingReconnectId;
        private int _historyIndex = -1;
        private List<string> _history = new();
        private bool _narrowLayout;
        private bool _uiReady;
        private bool _syncingSettings;
        private bool _syncingHistory;
        private bool _initialLoadDone;
        private int _tourStep = -1;

        public DevicesPage()
        {
            InitializeComponent();
            DeviceListView.ItemsSource = Devices;
            TerminalLogList.ItemsSource = _log.Entries;
            PresetsRepeater.ItemsSource = _presetChips;
            HistoryCombo.ItemsSource = _historyItems;
            ServiceCombo.ItemsSource = _serviceItems;
            Loaded += DevicesPage_Loaded;
            AppSettings.SettingsChanged += OnSettingsChanged;

            _watcher.DeviceAdded += OnDeviceAdded;
            _watcher.DeviceUpdated += OnDeviceUpdated;
            _watcher.DeviceRemoved += OnDeviceRemoved;
            _watcher.BleAdvertisementReceived += OnBleAdvertisement;
            _watcher.EnumerationCompleted += () => DispatcherQueue.TryEnqueue(() => ScanProgress.IsActive = false);
            _watcher.Stopped += () => DispatcherQueue.TryEnqueue(() =>
            {
                ScanProgress.IsActive = false;
                ScanButton.Content = _res.GetString("ScanButtonStart") ?? "Scan";
            });

            _session.DataReceived += bytes => DispatcherQueue.TryEnqueue(() =>
            {
                string msg = RfcommTerminalSession.FormatIncomingBytes(bytes);
                AppendTerminal(TrafficDirection.Rx, msg, bytes.Length);
            });
            _session.ConnectionLost += msg => DispatcherQueue.TryEnqueue(async () =>
            {
                AppendTerminal(TrafficDirection.Info, $"{_res.GetString("LogConnLost")}: {msg}");
                await HandleDisconnectAsync(tryReconnect: true);
            });
            _session.Disconnected += () => DispatcherQueue.TryEnqueue(() => UpdateConnectionUi(false));

            _server.StatusChanged += status => DispatcherQueue.TryEnqueue(() => UpdateServerStatus(status));
            _server.DataReceived += (clientId, bytes) => DispatcherQueue.TryEnqueue(() =>
            {
                string msg = RfcommTerminalSession.FormatIncomingBytes(bytes);
                AppendTerminal(TrafficDirection.Rx, $"[SERVER {clientId}] {msg}", bytes.Length);
            });
            _server.ClientConnected += id => DispatcherQueue.TryEnqueue(() =>
                AppendTerminal(TrafficDirection.Info, $"[SERVER] Client {id} connected (total {_server.ClientCount})"));
            _server.ClientDisconnected += id => DispatcherQueue.TryEnqueue(() =>
                AppendTerminal(TrafficDirection.Info, $"[SERVER] Client {id} disconnected"));

            _log.Changed += () => DispatcherQueue.TryEnqueue(() =>
            {
                RefreshByteCount();
                UpdateTerminalEmptyState();
                ScrollTerminalToEnd();
            });
        }

        private async void DevicesPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_initialLoadDone)
            {
                _initialLoadDone = true;
                HexToggle.IsOn = AppSettings.SendAsHex;
                SortCombo.SelectedIndex = (int)AppSettings.SortMode;
                ApplyCompactDensity();
                ReloadPresets();
                ReloadHistory();
                EnsureDefaultRfcommService();
                InitTeachingTipCopy();
                _uiReady = true;
                ApplyFilterAndSort();
                UpdateTerminalEmptyState();
                await CheckBluetoothAvailabilityAsync();
                UpdateEmptyState();
            }

            SyncConnectionHub();
            await TryShowDevicesTourAsync();
        }

        private async Task TryShowDevicesTourAsync()
        {
            if (!TeachingTourHelper.ShouldShowDevicesTour || _tourStep >= 0) return;
            await TeachingTourHelper.DelayForLayoutAsync();
            if (!TeachingTourHelper.ShouldShowDevicesTour || _tourStep >= 0) return;
            StartTeachingTour();
        }

        private void InitTeachingTipCopy()
        {
            string next = _res.GetString("TipNext") ?? "Next";
            string skip = _res.GetString("TipSkip") ?? "Skip";
            string done = _res.GetString("TipDone") ?? "Got it";

            TipScan.Title = _res.GetString("TipScanTitle") ?? "Scan for devices";
            TipScan.Subtitle = _res.GetString("TipScanSubtitle") ?? "Start a scan to find Classic and BLE devices nearby.";
            TipScan.ActionButtonContent = next;
            TipScan.CloseButtonContent = skip;

            TipDevices.Title = _res.GetString("TipDevicesTitle") ?? "Pair and connect";
            TipDevices.Subtitle = _res.GetString("TipDevicesSubtitle") ?? "Use Pair if needed, then Connect for the SPP terminal.";
            TipDevices.ActionButtonContent = next;
            TipDevices.CloseButtonContent = skip;

            TipTerminal.Title = _res.GetString("TipTerminalTitle") ?? "Send commands";
            TipTerminal.Subtitle = _res.GetString("TipTerminalSubtitle") ?? "Type here and press Send. TX/RX traffic appears in the log above.";
            TipTerminal.ActionButtonContent = next;
            TipTerminal.CloseButtonContent = skip;

            TipSession.Title = _res.GetString("TipSessionTitle") ?? "Session stays alive";
            TipSession.Subtitle = _res.GetString("TipSessionSubtitle") ?? "Switching tabs keeps your RFCOMM connection. Status is shown here and in the nav pane.";
            TipSession.ActionButtonContent = done;
            TipSession.CloseButtonContent = skip;
        }

        /// <summary>Starts or restarts the Devices TeachingTip sequence.</summary>
        public void StartTeachingTour()
        {
            InitTeachingTipCopy();
            _tourStep = 0;
            ShowTourStep();
        }

        private void ShowTourStep()
        {
            TipScan.IsOpen = false;
            TipDevices.IsOpen = false;
            TipTerminal.IsOpen = false;
            TipSession.IsOpen = false;

            switch (_tourStep)
            {
                case 0: TipScan.IsOpen = true; break;
                case 1: TipDevices.IsOpen = true; break;
                case 2: TipTerminal.IsOpen = true; break;
                case 3: TipSession.IsOpen = true; break;
                default:
                    TeachingTourHelper.MarkDevicesTourDone();
                    _tourStep = -1;
                    break;
            }
        }

        private void Tip_Next(TeachingTip sender, object args)
        {
            sender.IsOpen = false;
            _tourStep++;
            ShowTourStep();
        }

        private void Tip_Skip(TeachingTip sender, object args)
        {
            TipScan.IsOpen = false;
            TipDevices.IsOpen = false;
            TipTerminal.IsOpen = false;
            TipSession.IsOpen = false;
            TeachingTourHelper.MarkDevicesTourDone();
            _tourStep = -1;
        }

        private void SyncConnectionHub()
        {
            UpdateConnectionUi(_session.IsConnected);
            if (_server.IsRunning)
                UpdateServerStatus(_server.ClientCount > 0 ? "connected" : "listening");
        }

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (!_uiReady) return;
                _syncingSettings = true;
                try
                {
                    HexToggle.IsOn = AppSettings.SendAsHex;
                    if (SortCombo.SelectedIndex != (int)AppSettings.SortMode)
                        SortCombo.SelectedIndex = (int)AppSettings.SortMode;
                    ApplyCompactDensity();
                    ApplyFilterAndSort();
                }
                finally
                {
                    _syncingSettings = false;
                }
            });
        }

        private void ApplyCompactDensity()
        {
            bool compact = AppSettings.CompactDensity;
            DeviceListView.ItemTemplate = (DataTemplate)Resources[
                compact ? "DeviceCardCompactTemplate" : "DeviceCardTemplate"];
            ScanToolbar.Spacing = compact ? 6 : 8;
            TerminalToolbar.Spacing = compact ? 4 : 6;
            StatusChipRow.Spacing = compact ? 6 : 8;
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool narrow = e.NewSize.Width < 900;
            if (narrow == _narrowLayout) return;
            _narrowLayout = narrow;

            if (narrow)
            {
                LeftCol.Width = new GridLength(1, GridUnitType.Star);
                RightCol.Width = new GridLength(0);
                TopRow.Height = new GridLength(1, GridUnitType.Star);
                BottomRow.Height = new GridLength(1, GridUnitType.Star);
                Grid.SetColumn(DevicesPanel, 0);
                Grid.SetRow(DevicesPanel, 0);
                Grid.SetColumn(TerminalPanel, 0);
                Grid.SetRow(TerminalPanel, 1);
                TerminalPanel.Margin = new Thickness(0, 12, 0, 0);
                DevicesPanel.Margin = new Thickness(0);
            }
            else
            {
                LeftCol.Width = new GridLength(2, GridUnitType.Star);
                RightCol.Width = new GridLength(3, GridUnitType.Star);
                TopRow.Height = new GridLength(1, GridUnitType.Star);
                BottomRow.Height = new GridLength(0);
                Grid.SetColumn(DevicesPanel, 0);
                Grid.SetRow(DevicesPanel, 0);
                Grid.SetColumn(TerminalPanel, 1);
                Grid.SetRow(TerminalPanel, 0);
                TerminalPanel.Margin = new Thickness(12, 0, 0, 0);
                DevicesPanel.Margin = new Thickness(0, 0, 12, 0);
            }
        }

        private async Task<bool> CheckBluetoothAvailabilityAsync(bool showDialog = false)
        {
            try
            {
                _bluetoothAdapter = await BluetoothAdapter.GetDefaultAsync();
                if (_bluetoothAdapter == null)
                {
                    ScanButton.IsEnabled = false;
                    ShowBluetoothUnavailable(
                        ResHelper.Get(_res, "MsgBtErrorTitle", "Bluetooth unavailable"),
                        ResHelper.Get(_res, "MsgBtErrorContent", "No Bluetooth adapter was found on this device."));
                    if (showDialog)
                        await DialogHelper.ShowAsync(
                            ResHelper.Get(_res, "MsgBtErrorTitle", "Bluetooth unavailable"),
                            ResHelper.Get(_res, "MsgBtErrorContent", "No Bluetooth adapter was found on this device."));
                    return false;
                }

                if (!_bluetoothAdapter.IsCentralRoleSupported && !_bluetoothAdapter.IsPeripheralRoleSupported)
                {
                    ScanButton.IsEnabled = false;
                    ShowBluetoothUnavailable(
                        ResHelper.Get(_res, "MsgCompatErrorTitle", "Not supported"),
                        ResHelper.Get(_res, "MsgCompatErrorContent", "This Bluetooth adapter is not compatible."));
                    if (showDialog)
                        await DialogHelper.ShowAsync(
                            ResHelper.Get(_res, "MsgCompatErrorTitle", "Not supported"),
                            ResHelper.Get(_res, "MsgCompatErrorContent", "This Bluetooth adapter is not compatible."));
                    return false;
                }

                Windows.Devices.Radios.Radio? radio = null;
                try
                {
                    radio = await _bluetoothAdapter.GetRadioAsync();
                }
                catch (Exception)
                {
                    // GetRadioAsync can throw COMException when radio stack is missing/unavailable.
                    radio = null;
                }

                if (radio == null || radio.State != Windows.Devices.Radios.RadioState.On)
                {
                    ScanButton.IsEnabled = false;
                    ShowBluetoothUnavailable(
                        ResHelper.Get(_res, "MsgBtOffTitle", "Bluetooth is off"),
                        ResHelper.Get(_res, "MsgBtOffContent", "Turn on Bluetooth in Windows settings, then try again."));
                    if (showDialog)
                        await DialogHelper.ShowAsync(
                            ResHelper.Get(_res, "MsgBtOffTitle", "Bluetooth is off"),
                            ResHelper.Get(_res, "MsgBtOffContent", "Turn on Bluetooth in Windows settings, then try again."));
                    return false;
                }

                ScanButton.IsEnabled = true;
                ClearBluetoothUnavailable();
                return true;
            }
            catch (Exception ex)
            {
                ScanButton.IsEnabled = false;
                string title = ResHelper.Get(_res, "MsgInitErrorTitle", "Bluetooth error");
                string detail = string.IsNullOrWhiteSpace(ex.Message)
                    ? ResHelper.Get(_res, "MsgInitErrorContent", "Could not initialize Bluetooth.")
                    : $"{ResHelper.Get(_res, "MsgInitErrorContent", "Could not initialize Bluetooth.")} {ex.Message}";
                ShowBluetoothUnavailable(title, detail);
                if (showDialog)
                    await DialogHelper.ShowAsync(title, detail);
                return false;
            }
        }

        private void ShowBluetoothUnavailable(string title, string message)
        {
            // Hide adapter errors during Store screenshot tour / demo capture
            if (TeachingTourHelper.SuppressTips || StoreScreenshotTour.IsRunning)
            {
                ClearBluetoothUnavailable();
                return;
            }

            if (App.MainWindow is MainWindow main)
                main.ShowShellWarning(title, message);
        }

        private void ClearBluetoothUnavailable()
        {
            if (App.MainWindow is MainWindow main)
                main.ClearShellWarning();
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (!await CheckBluetoothAvailabilityAsync(showDialog: true)) return;

            if (_watcher.IsRunning)
            {
                _watcher.Stop(unsubscribe: false);
                ScanProgress.IsActive = false;
                ScanButton.Content = _res.GetString("ScanButtonStart") ?? "Scan";
                return;
            }

            Devices.Clear();
            _allDiscoveredDevices.Clear();
            ScanProgress.IsActive = true;
            ScanButton.Content = _res.GetString("ScanButtonStop") ?? "Stop";
            _watcher.Start();
            UpdateEmptyState();
        }

        private void OnDeviceAdded(DeviceInformation device)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                try { AddOrUpdateDevice(device); }
                catch { }
            });
        }

        private void AddOrUpdateDevice(DeviceInformation device)
        {
            if (string.IsNullOrEmpty(device.Id)) return;
            if (_allDiscoveredDevices.Any(d => d.Id == device.Id)) return;

            string mac = "";
            if (device.Properties.TryGetValue("System.Devices.Aep.DeviceAddress", out var macObj) && macObj != null)
                mac = NormalizeMac(macObj.ToString() ?? "");
            if (string.IsNullOrEmpty(mac))
                mac = TryExtractMacFromId(device.Id);

            BluetoothDeviceModel? existingByMac = null;
            if (!string.IsNullOrEmpty(mac))
            {
                existingByMac = _allDiscoveredDevices.FirstOrDefault(d =>
                    string.Equals(NormalizeMac(d.MacAddress), mac, StringComparison.OrdinalIgnoreCase));
            }

            string unknown = ResHelper.Get(_res, "UnknownDeviceLabel", "[Unknown Device]");
            string deviceName = !string.IsNullOrWhiteSpace(device.Name) ? device.Name : unknown;
            string idLower = device.Id.ToLowerInvariant();
            string bType = idLower.Contains("bluetoothle") || idLower.Contains("{bb7bb05e") ? "BLE" : "Classic";

            int? rssi = null;
            if (device.Properties.TryGetValue("System.Devices.Aep.SignalStrength", out var rssiObj) && rssiObj != null)
            {
                try { rssi = Convert.ToInt32(rssiObj); } catch { }
            }

            bool isPaired = false;
            try { isPaired = device.Pairing.IsPaired; }
            catch
            {
                if (device.Properties.TryGetValue("System.Devices.Aep.IsPaired", out var p) && p is bool b)
                    isPaired = b;
            }

            if (existingByMac != null && existingByMac.Id.StartsWith("ble-adv:", StringComparison.Ordinal))
            {
                existingByMac.Id = device.Id;
                if (!string.IsNullOrWhiteSpace(device.Name))
                    existingByMac.Name = deviceName;
                existingByMac.BluetoothType = bType;
                existingByMac.MacAddress = string.IsNullOrEmpty(mac) ? existingByMac.MacAddress : mac;
                existingByMac.IsPaired = isPaired;
                existingByMac.Type = isPaired
                    ? ResHelper.Get(_res, "StatusPaired", "Paired")
                    : ResHelper.Get(_res, "StatusAvailable", "Available");
                existingByMac.CanConnect = bType != "BLE";
                existingByMac.ConnectButtonText = ResHelper.Get(_res, "BtnConnect", "Connect");
                existingByMac.IsFavorite = _favorites.Contains(device.Id);
                if (rssi.HasValue) existingByMac.Rssi = rssi;
                existingByMac.LastSeen = DateTimeOffset.Now;
                if (_uiReady && PassesCurrentFilter(existingByMac) && !Devices.Contains(existingByMac))
                    Devices.Add(existingByMac);
                UpdateEmptyState();
                return;
            }

            if (existingByMac != null && !existingByMac.Id.StartsWith("ble-adv:", StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(device.Name) &&
                    existingByMac.Name.StartsWith("[", StringComparison.Ordinal))
                    existingByMac.Name = deviceName;
                if (rssi.HasValue) existingByMac.Rssi = rssi;
                if (isPaired) existingByMac.IsPaired = true;
                existingByMac.LastSeen = DateTimeOffset.Now;
                return;
            }

            var model = new BluetoothDeviceModel
            {
                Name = deviceName,
                Id = device.Id,
                BluetoothType = bType,
                MacAddress = mac,
                Type = isPaired
                    ? ResHelper.Get(_res, "StatusPaired", "Paired")
                    : ResHelper.Get(_res, "StatusAvailable", "Available"),
                IsPaired = isPaired,
                Rssi = rssi,
                ConnectButtonText = ResHelper.Get(_res, "BtnConnect", "Connect"),
                CanConnect = bType != "BLE",
                IsFavorite = _favorites.Contains(device.Id),
                LastSeen = DateTimeOffset.Now
            };

            _allDiscoveredDevices.Add(model);
            if (!_uiReady) return;
            if (PassesCurrentFilter(model))
                Devices.Add(model);
            UpdateEmptyState();
        }

        private void OnBleAdvertisement(BleAdvertisementHit hit)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    string mac = NormalizeMac(hit.MacAddress);
                    if (string.IsNullOrEmpty(mac)) return;

                    var existing = _allDiscoveredDevices.FirstOrDefault(d =>
                        string.Equals(NormalizeMac(d.MacAddress), mac, StringComparison.OrdinalIgnoreCase));

                    if (existing != null)
                    {
                        existing.Rssi = hit.Rssi;
                        existing.LastSeen = DateTimeOffset.Now;
                        if (!string.IsNullOrEmpty(hit.LocalName) &&
                            existing.Name.StartsWith("[", StringComparison.Ordinal))
                        {
                            existing.Name = hit.LocalName;
                            if (!_uiReady) return;
                            if (PassesCurrentFilter(existing) && !Devices.Contains(existing))
                                Devices.Add(existing);
                            else if (!PassesCurrentFilter(existing))
                            {
                                int idx = Devices.IndexOf(existing);
                                if (idx >= 0) Devices.RemoveAt(idx);
                            }
                        }
                        return;
                    }

                    string unknown = ResHelper.Get(_res, "UnknownDeviceLabel", "[Unknown Device]");
                    string name = string.IsNullOrEmpty(hit.LocalName) ? unknown : hit.LocalName;
                    var model = new BluetoothDeviceModel
                    {
                        Name = name,
                        Id = $"ble-adv:{mac}",
                        BluetoothType = "BLE",
                        MacAddress = mac,
                        RawBluetoothAddress = hit.BluetoothAddress,
                        Type = ResHelper.Get(_res, "StatusAvailable", "Available"),
                        IsPaired = false,
                        Rssi = hit.Rssi,
                        ConnectButtonText = ResHelper.Get(_res, "BtnConnect", "Connect"),
                        CanConnect = false,
                        IsFavorite = false,
                        LastSeen = DateTimeOffset.Now
                    };

                    _allDiscoveredDevices.Add(model);
                    if (!_uiReady) return;
                    if (PassesCurrentFilter(model))
                        Devices.Add(model);
                    UpdateEmptyState();
                }
                catch { }
            });
        }

        private void OnDeviceUpdated(DeviceInformationUpdate update)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    var model = _allDiscoveredDevices.FirstOrDefault(d => d.Id == update.Id);
                    if (model == null) return;

                    bool nameChanged = false;
                    if (update.Properties.TryGetValue("System.ItemNameDisplay", out var nameObj) &&
                        nameObj is string name && !string.IsNullOrWhiteSpace(name))
                    {
                        if (!string.Equals(model.Name, name, StringComparison.Ordinal))
                        {
                            model.Name = name;
                            nameChanged = true;
                        }
                    }

                    if (update.Properties.TryGetValue("System.Devices.Aep.SignalStrength", out var rssiObj) && rssiObj != null)
                        model.Rssi = Convert.ToInt32(rssiObj);

                    if (update.Properties.TryGetValue("System.Devices.Aep.DeviceAddress", out var macObj) && macObj != null)
                    {
                        string mac = NormalizeMac(macObj.ToString() ?? "");
                        if (!string.IsNullOrEmpty(mac))
                            model.MacAddress = mac;
                    }

                    if (update.Properties.TryGetValue("System.Devices.Aep.IsPaired", out var pairedObj) && pairedObj is bool paired)
                    {
                        model.IsPaired = paired;
                        model.Type = paired
                            ? ResHelper.Get(_res, "StatusPaired", "Paired")
                            : ResHelper.Get(_res, "StatusAvailable", "Available");
                    }

                    model.LastSeen = DateTimeOffset.Now;

                    if (nameChanged && _uiReady)
                    {
                        if (PassesCurrentFilter(model) && !Devices.Contains(model))
                            Devices.Add(model);
                        else if (!PassesCurrentFilter(model))
                        {
                            int idx = Devices.IndexOf(model);
                            if (idx >= 0) Devices.RemoveAt(idx);
                        }
                        UpdateEmptyState();
                    }
                }
                catch { }
            });
        }

        private static string NormalizeMac(string mac)
        {
            if (string.IsNullOrWhiteSpace(mac)) return "";
            string hex = mac.Replace("-", "").Replace(":", "").Replace(".", "").Trim().ToUpperInvariant();
            if (hex.Length != 12 || hex.Any(c => c is < '0' or > '9' and (< 'A' or > 'F')))
                return mac.Replace("-", ":").Trim().ToUpperInvariant();
            return string.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2)));
        }

        private static string TryExtractMacFromId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            var match = System.Text.RegularExpressions.Regex.Match(
                id, @"([0-9A-Fa-f]{2}[:\-]){5}[0-9A-Fa-f]{2}");
            return match.Success ? NormalizeMac(match.Value) : "";
        }
        private void OnDeviceRemoved(DeviceInformationUpdate update)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _allDiscoveredDevices.RemoveAll(d => d.Id == update.Id);
                for (int i = Devices.Count - 1; i >= 0; i--)
                {
                    if (Devices[i].Id == update.Id) Devices.RemoveAt(i);
                }
                UpdateEmptyState();
            });
        }

        private bool PassesCurrentFilter(BluetoothDeviceModel d)
        {
            string query = SearchBox?.Text?.Trim() ?? "";
            string unknown = ResHelper.Get(_res, "UnknownDeviceLabel", "Unknown");
            bool showUnknown = ShowUnknownToggle?.IsOn ?? true;

            bool namedOk = d.Name != unknown || showUnknown;
            if (!namedOk) return false;
            if (string.IsNullOrEmpty(query)) return true;
            return (d.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (d.MacAddress?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private void ApplyFilterAndSort()
        {
            // SortCombo/ShowUnknownToggle fire during InitializeComponent before EmptyStatePanel exists
            if (!_uiReady || EmptyStatePanel == null || DeviceListView == null) return;

            IEnumerable<BluetoothDeviceModel> filtered = _allDiscoveredDevices.Where(PassesCurrentFilter);

            filtered = AppSettings.SortMode switch
            {
                DeviceSortMode.Name => filtered.OrderByDescending(d => d.IsFavorite).ThenBy(d => d.Name),
                DeviceSortMode.RecentlySeen => filtered.OrderByDescending(d => d.IsFavorite).ThenByDescending(d => d.LastSeen),
                _ => filtered.OrderByDescending(d => d.IsFavorite).ThenByDescending(d => d.Rssi ?? -999)
            };

            var list = filtered.ToList();
            string? selectedId = (DeviceListView.SelectedItem as BluetoothDeviceModel)?.Id;
            SyncDeviceList(list);
            if (selectedId != null)
                DeviceListView.SelectedItem = Devices.FirstOrDefault(d => d.Id == selectedId);
            UpdateEmptyState();
        }

        /// <summary>
        /// Rebuilds the visible list without Clear() when possible, so existing rows stay in place.
        /// </summary>
        private void SyncDeviceList(List<BluetoothDeviceModel> desired)
        {
            for (int i = Devices.Count - 1; i >= 0; i--)
            {
                if (!desired.Contains(Devices[i]))
                    Devices.RemoveAt(i);
            }

            for (int i = 0; i < desired.Count; i++)
            {
                var item = desired[i];
                int current = Devices.IndexOf(item);
                if (current < 0)
                    Devices.Insert(Math.Min(i, Devices.Count), item);
                else if (current != i)
                    Devices.Move(current, i);
            }
        }

        private void UpdateEmptyState()
        {
            if (EmptyStatePanel == null) return;
            EmptyStatePanel.Visibility = Devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateTerminalEmptyState()
        {
            if (TerminalEmptyState == null) return;
            bool empty = _log.Entries.Count == 0 && !_session.IsConnected && !_server.IsRunning;
            TerminalEmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ScrollTerminalToEnd()
        {
            if (TerminalLogList.Items.Count == 0) return;
            TerminalLogList.ScrollIntoView(TerminalLogList.Items[^1]);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilterAndSort();

        private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_uiReady || _syncingSettings) return;
            if (SortCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                AppSettings.SortMode = tag switch
                {
                    "Name" => DeviceSortMode.Name,
                    "Recent" => DeviceSortMode.RecentlySeen,
                    _ => DeviceSortMode.Rssi
                };
                AppSettings.NotifyChanged();
                ApplyFilterAndSort();
            }
        }

        private void ShowUnknownToggle_Toggled(object sender, RoutedEventArgs e) => ApplyFilterAndSort();

        private void HexToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_uiReady || _syncingSettings) return;
            AppSettings.SendAsHex = HexToggle.IsOn;
            AppSettings.NotifyChanged();
        }

        private void Favorite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string id) return;
            bool added = FavoritesStore.Toggle(id);
            if (added) _favorites.Add(id); else _favorites.Remove(id);
            var model = _allDiscoveredDevices.FirstOrDefault(d => d.Id == id);
            if (model != null) model.IsFavorite = added;
            ApplyFilterAndSort();
        }

        private async void Pair_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string id) return;
            btn.IsEnabled = false;
            try
            {
                var info = await DeviceInformation.CreateFromIdAsync(id);
                if (info == null)
                {
                    await DialogHelper.ShowAsync(_res.GetString("MsgConnErrorTitle"), _res.GetString("ErrorDeviceAccess"));
                    return;
                }

                if (info.Pairing.IsPaired)
                {
                    var result = await info.Pairing.UnpairAsync();
                    if (result.Status != DeviceUnpairingResultStatus.Unpaired &&
                        result.Status != DeviceUnpairingResultStatus.AlreadyUnpaired)
                    {
                        await DialogHelper.ShowAsync(_res.GetString("PairErrorTitle"), result.Status.ToString());
                    }
                }
                else
                {
                    var result = await info.Pairing.PairAsync();
                    if (result.Status != DevicePairingResultStatus.Paired &&
                        result.Status != DevicePairingResultStatus.AlreadyPaired)
                    {
                        await DialogHelper.ShowAsync(_res.GetString("PairErrorTitle"),
                            $"{_res.GetString("PairErrorContent")} {result.Status}");
                    }
                }

                var model = _allDiscoveredDevices.FirstOrDefault(d => d.Id == id);
                if (model != null)
                {
                    model.IsPaired = info.Pairing.IsPaired;
                    model.Type = model.IsPaired ? _res.GetString("StatusPaired") : _res.GetString("StatusAvailable");
                }
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowAsync(_res.GetString("PairErrorTitle"), ex.Message);
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string deviceId) return;
            button.IsEnabled = false;
            StatusTextBlock.Text = _res.GetString("StatusConnecting");

            try
            {
                await LoadServicesForDeviceAsync(deviceId);
                int serviceIndex = ServiceCombo.SelectedIndex >= 0 ? ServiceCombo.SelectedIndex : 0;

                await _session.ConnectAsync(deviceId, serviceIndex);
                AppSettings.LastConnectedDeviceId = deviceId;
                _pendingReconnectId = deviceId;

                StatusTextBlock.Text = $"{_res.GetString("StatusConnectedTo")} {_session.ConnectedDeviceName}";
                UpdateConnectionUi(true);
                AppendTerminal(TrafficDirection.Info, StatusTextBlock.Text);
                await RateManager.RegisterMeaningfulEngageAsync();
            }
            catch (Exception ex)
            {
                _session.Disconnect(silent: true);
                StatusTextBlock.Text = _res.GetString("StatusConnError");
                string msg = ex.Message switch
                {
                    "device_access" => _res.GetString("ErrorDeviceAccess"),
                    "no_spp" => _res.GetString("ErrorNoSppProfile"),
                    _ => ex.Message
                };
                await DialogHelper.ShowAsync(_res.GetString("MsgConnErrorTitle"), msg);
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private async Task LoadServicesForDeviceAsync(string deviceId)
        {
            _serviceItems.Clear();
            try
            {
                using var device = await BluetoothDevice.FromIdAsync(deviceId);
                if (device == null)
                {
                    EnsureDefaultRfcommService();
                    return;
                }

                var names = new List<string>();
                await CollectRfcommNamesAsync(device, names, BluetoothCacheMode.Uncached);
                if (names.Count == 0)
                    await CollectRfcommNamesAsync(device, names, BluetoothCacheMode.Cached);

                if (!names.Any(n => n.Contains("SPP", StringComparison.OrdinalIgnoreCase) ||
                                    n.Contains("Serial", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        var spp = await device.GetRfcommServicesForIdAsync(
                            RfcommServiceId.SerialPort, BluetoothCacheMode.Cached);
                        AppendRfcommNames(spp, names);
                    }
                    catch { }
                }

                if (names.Count == 0)
                {
                    EnsureDefaultRfcommService();
                    return;
                }

                foreach (var name in names)
                    _serviceItems.Add(name);
                ServiceCombo.SelectedIndex = 0;
            }
            catch
            {
                EnsureDefaultRfcommService();
            }
        }

        private static async Task CollectRfcommNamesAsync(
            BluetoothDevice device, List<string> names, BluetoothCacheMode cacheMode)
        {
            try
            {
                var result = await device.GetRfcommServicesAsync(cacheMode);
                AppendRfcommNames(result, names);
            }
            catch { }
        }

        private static void AppendRfcommNames(RfcommDeviceServicesResult? result, List<string> names)
        {
            if (result?.Services == null) return;
            foreach (var svc in result.Services)
            {
                string name;
                try
                {
                    name = svc.ServiceId == RfcommServiceId.SerialPort
                        ? ResHelper.Get("ServiceSppDefault", "Serial Port (SPP)")
                        : svc.ServiceId.AsString();
                }
                catch
                {
                    name = "RFCOMM";
                }

                if (names.Any(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase)))
                    continue;
                names.Add($"[{names.Count}] {name}");
            }
        }

        private void EnsureDefaultRfcommService()
        {
            if (_serviceItems.Count == 0)
                _serviceItems.Add("[0] " + ResHelper.Get("ServiceSppDefault", "Serial Port (SPP)"));
            if (ServiceCombo.SelectedIndex < 0 && _serviceItems.Count > 0)
                ServiceCombo.SelectedIndex = 0;
        }

        private async void DeviceListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DeviceListView.SelectedItem is BluetoothDeviceModel model && model.CanConnect)
                await LoadServicesForDeviceAsync(model.Id);
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            _pendingReconnectId = null;
            _ = HandleDisconnectAsync(tryReconnect: false);
        }

        private async Task HandleDisconnectAsync(bool tryReconnect)
        {
            _session.Disconnect(silent: true);
            UpdateConnectionUi(false);
            StatusTextBlock.Text = _res.GetString("StatusDisconnected");

            if (tryReconnect && AppSettings.AutoReconnect && !string.IsNullOrEmpty(_pendingReconnectId))
            {
                AppendTerminal(TrafficDirection.Info, _res.GetString("StatusReconnecting") ?? "Reconnecting...");
                await Task.Delay(1500);
                try
                {
                    await _session.ConnectAsync(_pendingReconnectId!, _session.SelectedServiceIndex);
                    StatusTextBlock.Text = $"{_res.GetString("StatusConnectedTo")} {_session.ConnectedDeviceName}";
                    UpdateConnectionUi(true);
                    AppendTerminal(TrafficDirection.Info, StatusTextBlock.Text);
                }
                catch (Exception ex)
                {
                    AppendTerminal(TrafficDirection.Info, $"Reconnect failed: {ex.Message}");
                }
            }
        }

        private void UpdateConnectionUi(bool connected)
        {
            bool canSend = connected || _server.ClientCount > 0;
            SendButton.IsEnabled = canSend;
            DisconnectButton.IsEnabled = connected;
            ConnectionDot.Fill = ThemeHelper.GetBrush(
                connected ? "StatusConnectedBrush" : "StatusDisconnectedBrush");
            ConnectionStatusHub.Set(connected, connected ? _session.ConnectedDeviceName : null);
            UpdateTerminalEmptyState();
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string message = InputTextBox.Text;
            if (string.IsNullOrWhiteSpace(message)) return;

            try
            {
                byte[] payload;
                bool asHex = HexToggle.IsOn || AppSettings.SendAsHex;
                if (asHex)
                {
                    if (!RfcommTerminalSession.TryParseHex(message, out payload))
                    {
                        AppendTerminal(TrafficDirection.Info, _res.GetString("ErrorInvalidHex") ?? "Invalid hex");
                        return;
                    }
                }
                else
                {
                    payload = System.Text.Encoding.UTF8.GetBytes(message + AppSettings.GetLineEndingString());
                }

                if (_session.IsConnected)
                    await _session.SendAsync(payload);
                else if (_server.IsRunning)
                    await _server.BroadcastAsync(asHex ? payload : System.Text.Encoding.UTF8.GetBytes(message + AppSettings.GetLineEndingString()));
                else
                    return;

                CommandPresetStore.PushHistory(message);
                ReloadHistory();
                AppendTerminal(TrafficDirection.Tx, message, payload.Length);
                InputTextBox.Text = string.Empty;
            }
            catch (Exception ex)
            {
                AppendTerminal(TrafficDirection.Info, $"{_res.GetString("LogSendError")}: {ex.Message}");
            }
        }

        private void InputTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                SendButton_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Up)
            {
                if (_history.Count == 0) return;
                _historyIndex = Math.Min(_history.Count - 1, _historyIndex + 1);
                InputTextBox.Text = _history[_historyIndex];
                InputTextBox.SelectionStart = InputTextBox.Text.Length;
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Down)
            {
                if (_historyIndex <= 0)
                {
                    _historyIndex = -1;
                    InputTextBox.Text = "";
                }
                else
                {
                    _historyIndex--;
                    InputTextBox.Text = _history[_historyIndex];
                }
                e.Handled = true;
            }
        }

        private async void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsProUser)
            {
                await LicenseManager.CheckLicenseAsync(startTrialIfNeeded: true);
                if (!LicenseManager.IsProUser)
                {
                    var dialog = new ContentDialog
                    {
                        Title = _res.GetString("ServerLockTitle") ?? "PRO Feature",
                        Content = _res.GetString("ServerLockContent"),
                        PrimaryButtonText = _res.GetString("BtnBuyPro") ?? "Buy PRO",
                        CloseButtonText = _res.GetString("Cancel") ?? "Cancel",
                        XamlRoot = App.MainWindow.Content.XamlRoot
                    };
                    var result = await dialog.ShowAsync();
                    if (result != ContentDialogResult.Primary) return;
                    if (!await LicenseManager.RequestPurchaseAsync()) return;
                    await DialogHelper.ShowAsync(_res.GetString("ProPurchaseSuccessTitle"), _res.GetString("ProPurchaseSuccessContent"));
                    await RateManager.RegisterMeaningfulEngageAsync();
                }
            }

            if (!await CheckBluetoothAvailabilityAsync(showDialog: true)) return;

            try
            {
                StartServerButton.IsEnabled = false;
                await _server.StartAsync();
                StopServerButton.IsEnabled = true;
                AppendTerminal(TrafficDirection.Info, $"[SERVER]: {_res.GetString("StatusServerListening")}");
            }
            catch (Exception ex)
            {
                StartServerButton.IsEnabled = true;
                await DialogHelper.ShowAsync(_res.GetString("ServerErrorTitle") ?? "Server Error", ex.Message);
            }
        }

        private void StopServerButton_Click(object sender, RoutedEventArgs e)
        {
            _server.Stop();
            StartServerButton.IsEnabled = true;
            StopServerButton.IsEnabled = false;
            UpdateConnectionUi(_session.IsConnected);
            ServerStatusTextBlock.Text = _res.GetString("StatusServerStopped");
        }

        private void UpdateServerStatus(string status)
        {
            ServerStatusTextBlock.Text = status switch
            {
                "listening" => _res.GetString("StatusServerListening"),
                "connected" => $"{_res.GetString("StatusServerConnected")} ({_server.ClientCount})",
                "stopped" => _res.GetString("StatusServerStopped"),
                _ => status
            };
            UpdateConnectionUi(_session.IsConnected);
            UpdateTerminalEmptyState();
        }

        private void AppendTerminal(TrafficDirection dir, string message, int bytes = 0)
        {
            _log.Append(dir, message, bytes);
        }

        private void RefreshByteCount()
        {
            ByteCountText.Text = $"TX {_log.TotalBytesTx} / RX {_log.TotalBytesRx} B";
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            _log.Clear();
            UpdateTerminalEmptyState();
        }

        private void CopyLog_Click(object sender, RoutedEventArgs e)
        {
            var data = new DataPackage();
            data.SetText(_log.ToPlainText());
            Clipboard.SetContent(data);
        }

        private void ReloadPresets()
        {
            _presetChips.Clear();
            foreach (var p in CommandPresetStore.LoadPresets().Take(8))
                _presetChips.Add(new PresetChip { Id = p.Id, Name = p.Name });
        }

        private void ReloadHistory()
        {
            _history = CommandPresetStore.LoadHistory();
            _historyIndex = -1;
            _syncingHistory = true;
            try
            {
                _historyItems.Clear();
                foreach (var h in _history.Take(20))
                    _historyItems.Add(h);
                HistoryCombo.SelectedItem = null;
            }
            finally
            {
                _syncingHistory = false;
            }
        }

        private async void PresetChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string id) return;
            var preset = CommandPresetStore.LoadPresets().FirstOrDefault(p => p.Id == id);
            if (preset == null) return;
            InputTextBox.Text = preset.Payload;
            HexToggle.IsOn = preset.AsHex;
            if (_session.IsConnected || _server.ClientCount > 0)
                SendButton_Click(this, new RoutedEventArgs());
            await Task.CompletedTask;
        }

        private async void AddPreset_Click(object sender, RoutedEventArgs e)
        {
            var nameBox = new TextBox
            {
                PlaceholderText = ResHelper.Get(_res, "PresetNamePlaceholder", "Preset name"),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var payloadBox = new TextBox
            {
                PlaceholderText = ResHelper.Get(_res, "PresetPayloadPlaceholder", "Command / payload"),
                Text = InputTextBox.Text ?? ""
            };
            var panel = new StackPanel();
            panel.Children.Add(nameBox);
            panel.Children.Add(payloadBox);

            var dialog = new ContentDialog
            {
                Title = ResHelper.Get(_res, "AddPresetTitle", "Add preset"),
                Content = panel,
                PrimaryButtonText = ResHelper.Get(_res, "DialogOk", "OK"),
                CloseButtonText = ResHelper.Get(_res, "Cancel", "Cancel"),
                XamlRoot = App.MainWindow.Content.XamlRoot
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            string payload = payloadBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(payload))
            {
                await DialogHelper.ShowAsync(
                    ResHelper.Get(_res, "AddPresetTitle", "Add preset"),
                    ResHelper.Get(_res, "PresetEmptyMessage", "Enter a command to save as a preset."));
                return;
            }

            var presets = CommandPresetStore.LoadPresets();
            presets.Add(new CommandPreset
            {
                Name = string.IsNullOrWhiteSpace(nameBox.Text) ? payload : nameBox.Text.Trim(),
                Payload = payload,
                AsHex = HexToggle.IsOn
            });
            CommandPresetStore.SavePresets(presets);
            ReloadPresets();
        }

        private async void RunMacro_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsProUser)
            {
                await LicenseManager.CheckLicenseAsync(startTrialIfNeeded: true);
                if (!LicenseManager.IsProUser)
                {
                    var dialog = new ContentDialog
                    {
                        Title = ResHelper.Get(_res, "ServerLockTitle", "Available in BlueIDE PRO"),
                        Content = ResHelper.Get(_res, "MacroProOnly", "Command macros require BlueIDE PRO."),
                        PrimaryButtonText = ResHelper.Get(_res, "BtnBuyPro", "Buy PRO"),
                        CloseButtonText = ResHelper.Get(_res, "Cancel", "Cancel"),
                        XamlRoot = App.MainWindow.Content.XamlRoot
                    };
                    if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
                    if (!await LicenseManager.RequestPurchaseAsync()) return;
                    await DialogHelper.ShowAsync(
                        ResHelper.Get(_res, "ProPurchaseSuccessTitle", "Success!"),
                        ResHelper.Get(_res, "ProPurchaseSuccessContent", "PRO unlocked."));
                    await RateManager.RegisterMeaningfulEngageAsync();
                }
            }

            var presets = CommandPresetStore.LoadPresets();
            if (presets.Count == 0)
            {
                await DialogHelper.ShowAsync(
                    ResHelper.Get(_res, "BtnRunMacro.Content", "Run macro"),
                    ResHelper.Get(_res, "MacroNoPresets", "Add at least one preset before running a macro."));
                return;
            }

            if (!_session.IsConnected && _server.ClientCount == 0)
            {
                await DialogHelper.ShowAsync(
                    ResHelper.Get(_res, "BtnRunMacro.Content", "Run macro"),
                    ResHelper.Get(_res, "MacroNotConnected", "Connect to a device or start the echo server first."));
                return;
            }

            foreach (var p in presets)
            {
                InputTextBox.Text = p.Payload;
                HexToggle.IsOn = p.AsHex;
                SendButton_Click(this, new RoutedEventArgs());
                if (p.DelayMs > 0) await Task.Delay(p.DelayMs);
                else await Task.Delay(200);
            }
        }

        private void HistoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingHistory) return;
            if (HistoryCombo.SelectedItem is string s)
                InputTextBox.Text = s;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (_uiReady)
            {
                SyncConnectionHub();
                _ = TryShowDevicesTourAsync();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            // Keep RFCOMM session and server alive while the page is cached.
            // Pause discovery only to reduce background work.
            if (_watcher.IsRunning)
            {
                _watcher.Stop(unsubscribe: false);
                ScanProgress.IsActive = false;
                ScanButton.Content = _res.GetString("ScanButtonStart") ?? "Scan";
            }
            base.OnNavigatedFrom(e);
        }

        public void ApplyDemoScene(string sceneFileName)
        {
            ClearBluetoothUnavailable();

            string btnConnect = ResHelper.Get(_res, "BtnConnect", "Connect");
            string statusAvailable = ResHelper.Get(_res, "StatusAvailable", "Available");
            string statusPaired = ResHelper.Get(_res, "StatusPaired", "Paired");

            _allDiscoveredDevices.Clear();
            Devices.Clear();
            _log.Clear();

            var demoDevices = new[]
            {
                new BluetoothDeviceModel
                {
                    Id = "demo-esp32", Name = "ESP32-Weather-Node", BluetoothType = "BLE",
                    MacAddress = "34:94:70:AA:BB:CC", Type = statusAvailable, Rssi = -41,
                    ConnectButtonText = btnConnect, CanConnect = true, IsFavorite = true, BatteryLevel = 87
                },
                new BluetoothDeviceModel
                {
                    Id = "demo-arduino", Name = "Arduino-Robot-Arm", BluetoothType = "Classic",
                    MacAddress = "00:14:03:05:5A:11", Type = statusPaired, Rssi = -55,
                    ConnectButtonText = btnConnect, CanConnect = true, IsPaired = true, IsFavorite = true
                },
                new BluetoothDeviceModel
                {
                    Id = "demo-nrf", Name = "nRF52840-DK", BluetoothType = "BLE",
                    MacAddress = "D0:B5:C2:11:22:33", Type = statusAvailable, Rssi = -62,
                    ConnectButtonText = btnConnect, CanConnect = true, BatteryLevel = 64
                },
                new BluetoothDeviceModel
                {
                    Id = "demo-hm10", Name = "HM-10-UART", BluetoothType = "BLE",
                    MacAddress = "A4:C1:38:9F:01:02", Type = statusAvailable, Rssi = -73,
                    ConnectButtonText = btnConnect, CanConnect = true
                },
                new BluetoothDeviceModel
                {
                    Id = "demo-hc05", Name = "HC-05-SPP", BluetoothType = "Classic",
                    MacAddress = "98:D3:31:F5:A0:10", Type = statusPaired, Rssi = -68,
                    ConnectButtonText = btnConnect, CanConnect = true, IsPaired = true
                },
                new BluetoothDeviceModel
                {
                    Id = "demo-pixel", Name = "Pixel Buds", BluetoothType = "BLE",
                    MacAddress = "B8:27:EB:DE:AD:01", Type = statusAvailable, Rssi = -81,
                    ConnectButtonText = btnConnect, CanConnect = false, BatteryLevel = 42
                },
            };

            foreach (var d in demoDevices)
                _allDiscoveredDevices.Add(d);
            ApplyFilterAndSort();

            // In-memory presets (do not overwrite user store)
            _presetChips.Clear();
            _presetChips.Add(new PresetChip { Id = "d1", Name = "AT" });
            _presetChips.Add(new PresetChip { Id = "d2", Name = "STATUS" });
            _presetChips.Add(new PresetChip { Id = "d3", Name = "0x01 0xFF" });
            _presetChips.Add(new PresetChip { Id = "d4", Name = "PING" });

            bool hex = sceneFileName.StartsWith("03-", StringComparison.Ordinal);
            HexToggle.IsOn = hex;
            InputTextBox.Text = hex ? "01 03 00 00 00 0A" : "STATUS";
            InputTextBox.IsEnabled = true;
            SendButton.IsEnabled = true;

            bool serverScene = sceneFileName.StartsWith("05-", StringComparison.Ordinal);
            if (serverScene)
            {
                UpdateServerStatus("connected");
                StartServerButton.IsEnabled = false;
                StopServerButton.IsEnabled = true;
                AppendTerminal(TrafficDirection.Info, "[SERVER]: " + (ResHelper.Get(_res, "StatusServerListening", "Listening")));
                AppendTerminal(TrafficDirection.Info, "[SERVER] Client 1 connected (total 1)");
                AppendTerminal(TrafficDirection.Rx, "[SERVER 1] PING", 4);
                AppendTerminal(TrafficDirection.Tx, "PONG", 4);
                StatusTextBlock.Text = ResHelper.Get(_res, "StatusDisconnected", "Disconnected");
                DisconnectButton.IsEnabled = false;
                ConnectionDot.Fill = ThemeHelper.GetBrush("StatusDisconnectedBrush");
                ConnectionStatusHub.Clear();
            }
            else
            {
                StartServerButton.IsEnabled = true;
                StopServerButton.IsEnabled = false;
                UpdateServerStatus("stopped");

                string connectedTo = ResHelper.Get(_res, "StatusConnectedTo", "Connected to");
                StatusTextBlock.Text = $"{connectedTo} ESP32-Weather-Node";
                DisconnectButton.IsEnabled = true;
                ConnectionDot.Fill = ThemeHelper.GetBrush("StatusConnectedBrush");
                ConnectionStatusHub.Set(true, "ESP32-Weather-Node");

                AppendTerminal(TrafficDirection.Info, StatusTextBlock.Text);
                if (hex)
                {
                    AppendTerminal(TrafficDirection.Tx, "01 03 00 00 00 0A", 6);
                    AppendTerminal(TrafficDirection.Rx, "01 03 14 00 64 00 C8", 7);
                    AppendTerminal(TrafficDirection.Tx, "01 FF AA 55", 4);
                    AppendTerminal(TrafficDirection.Rx, "ACK 55 AA", 6);
                }
                else
                {
                    AppendTerminal(TrafficDirection.Tx, "STATUS", 6);
                    AppendTerminal(TrafficDirection.Rx, "OK temp=23.4 humidity=41", 24);
                    AppendTerminal(TrafficDirection.Tx, "AT+NAME?", 8);
                    AppendTerminal(TrafficDirection.Rx, "+NAME:ESP32-Weather-Node", 24);
                }
            }

            if (sceneFileName.StartsWith("02-", StringComparison.Ordinal))
            {
                ScanProgress.IsActive = true;
                ScanButton.Content = ResHelper.Get(_res, "ScanButtonStop", "Stop");
            }
            else
            {
                ScanProgress.IsActive = false;
                ScanButton.Content = ResHelper.Get(_res, "ScanButtonStart", "Scan");
            }

            RefreshByteCount();
            UpdateTerminalEmptyState();
        }

        public void LoadDemoDataForScreenshots(int step)
            => ApplyDemoScene(step <= 1 ? "01-devices-terminal.png" : "02-ble-scan.png");
    }
}









