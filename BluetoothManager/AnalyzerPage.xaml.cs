using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BluetoothManager.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace BluetoothManager
{
    public sealed partial class AnalyzerPage : Page
    {
        private readonly ResourceLoader _res = new();
        public ObservableCollection<BluetoothDeviceModel> BleDevices { get; } = new();
        private readonly List<BluetoothDeviceModel> _allBle = new();
        public ObservableCollection<BleServiceModel> BleServices { get; } = new();
        private readonly TrafficLogStore _traffic = new();
        private readonly Dictionary<string, TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs>> _notifyHandlers = new();

        private BluetoothLEDevice? _currentBleDevice;
        private CancellationTokenSource? _gattCts;
        private CancellationTokenSource? _scriptCts;
        private int _gattOpId;
        private BluetoothLEAdvertisementWatcher? _bleAdvWatcher;
        private readonly HashSet<string> _detectedMacs = new();
        private readonly object _macLock = new();
        private BleCharacteristicModel? _selectedChar;
        private readonly List<BleCharacteristicModel> _allCharacteristics = new();
        private bool _initialLoadDone;

        public AnalyzerPage()
        {
            InitializeComponent();
            BleDeviceListView.ItemsSource = BleDevices;
            BleDevicesHeader.Text = ResHelper.Get(_res, "BleDevicesHeader", "BLE Devices");
            AnalyzerHeader.Text = ResHelper.Get(_res, "AnalyzerHeader", "GATT Services");
            CharacteristicsHeader.Text = ResHelper.Get(_res, "CharacteristicsHeader", "Characteristics");
            Loaded += AnalyzerPage_Loaded;
            LicenseManager.StatusChanged += OnLicenseStatusChanged;
            _traffic.Changed += () => DispatcherQueue.TryEnqueue(() =>
            {
                TrafficByteCount.Text = $"TX {_traffic.TotalBytesTx} / RX {_traffic.TotalBytesRx} B";
            });
        }

        private async void AnalyzerPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_initialLoadDone)
            {
                Loaded -= AnalyzerPage_Loaded;
                _initialLoadDone = true;
                await LicenseManager.CheckLicenseAsync(startTrialIfNeeded: true);
                UpdateUiState();
                UpdateAnalyzerEmptyStates();
                SetCharSelectionVisible(false);
                InitAnalyzerTipCopy();
            }

            if (LicenseManager.IsProUser && TeachingTourHelper.ShouldShowAnalyzerTip
                && AnalyzerPanel.Visibility == Visibility.Visible)
            {
                await TeachingTourHelper.DelayForLayoutAsync();
                TipAnalyzer.IsOpen = true;
            }
        }

        private void InitAnalyzerTipCopy()
        {
            TipAnalyzer.Title = ResHelper.Get(_res, "TipAnalyzerTitle", "BLE Analyzer");
            TipAnalyzer.Subtitle = ResHelper.Get(_res, "TipAnalyzerSubtitle",
                "Scan for BLE advertisers, pick a device, then explore GATT services and characteristics.");
            TipAnalyzer.ActionButtonContent = ResHelper.Get(_res, "TipDone", "Got it");
            TipAnalyzer.CloseButtonContent = ResHelper.Get(_res, "TipSkip", "Skip");
        }

        private void AnalyzerTip_Done(TeachingTip sender, object args)
        {
            TipAnalyzer.IsOpen = false;
            TeachingTourHelper.MarkAnalyzerTipDone();
        }

        private void UpdateUiState()
        {
            // Screenshot tour seeds Analyzer UI regardless of license.
            bool unlock = LicenseManager.IsProUser || TeachingTourHelper.SuppressTips;
            if (unlock)
            {
                PurchasePanel.Visibility = Visibility.Collapsed;
                AnalyzerPanel.Visibility = Visibility.Visible;
                if (!TeachingTourHelper.SuppressTips)
                    UpdateTrialBanner();
                else
                    TrialBanner.Visibility = Visibility.Collapsed;
            }
            else
            {
                TrialBanner.Visibility = Visibility.Collapsed;
                PurchasePanel.Visibility = Visibility.Visible;
                AnalyzerPanel.Visibility = Visibility.Collapsed;
                ApplyExpiredPaywallCopy();
            }

            _ = ApplyPriceAsync();
        }

        /// <summary>Puts the Store price on buy buttons ("Buy PRO — $4.99") and shows a sale line if any.</summary>
        private async Task ApplyPriceAsync()
        {
            if (LicenseManager.AccessKind == ProAccessKind.Purchased) return;

            // Plain label first so the button is never empty while the Store query runs.
            if (TrialBuyButton.Content == null)
                TrialBuyButton.Content = ProGate.BuyButtonText(null);

            string buyText = await ProGate.GetBuyButtonTextAsync();
            string? saleText = await ProGate.GetSaleTextAsync();

            UnlockProButton.Content = buyText;
            TrialBuyButton.Content = buyText;
            SalePriceText.Text = saleText ?? "";
            SalePriceText.Visibility = saleText == null ? Visibility.Collapsed : Visibility.Visible;
        }

        private void UpdateTrialBanner()
        {
            if (!LicenseManager.IsTrialActive)
            {
                TrialBanner.Visibility = Visibility.Collapsed;
                return;
            }

            TrialBannerTitleText.Text = ResHelper.Get(_res, "TrialBannerTitle", "PRO trial");
            string template = ResHelper.Get(_res, "TrialBannerMessage",
                "{0} day(s) left. Buy PRO to keep Analyzer, echo server, and macros.");
            TrialBannerMessageText.Text = string.Format(template, LicenseManager.TrialDaysRemaining);
            TrialBanner.Visibility = Visibility.Visible;
        }

        private void ApplyExpiredPaywallCopy()
        {
            if (LicenseManager.IsTrialExpired)
            {
                PurchaseTitle.Text = ResHelper.Get(_res, "TrialEndedTitle", "PRO trial ended");
                PurchaseSubtitle.Text = ResHelper.Get(_res, "TrialEndedContent",
                    "Your 14-day PRO trial has ended. Unlock PRO to keep using the BLE Analyzer and advanced tools.");
                AnalyzerPromoTextBlock.Text = ResHelper.Get(_res, "TrialEndedContent",
                    "Your 14-day PRO trial has ended. Unlock PRO to keep using the BLE Analyzer and advanced tools.");
            }
        }

        private async void TrialInfoBar_BuyClick(object sender, RoutedEventArgs e)
        {
            await PurchaseProAsync();
        }

        private async void UnlockProButton_Click(object sender, RoutedEventArgs e)
        {
            UnlockProButton.IsEnabled = false;
            bool success = await PurchaseProAsync();
            if (!success)
                UnlockProButton.IsEnabled = true;
        }

        private async Task<bool> PurchaseProAsync()
        {
            if (!await ProGate.PurchaseAsync())
                return false;

            UpdateUiState();
            if (TeachingTourHelper.ShouldShowAnalyzerTip && AnalyzerPanel.Visibility == Visibility.Visible)
            {
                await TeachingTourHelper.DelayForLayoutAsync();
                TipAnalyzer.IsOpen = true;
            }
            return true;
        }

        private async void ScanBleButton_Click(object sender, RoutedEventArgs e)
        {
            BleDevices.Clear();
            _allBle.Clear();
            BleServices.Clear();
            GattTree.RootNodes.Clear();
            _detectedMacs.Clear();
            _allCharacteristics.Clear();

            ScanBleButton.IsEnabled = false;
            AnalyzerProgress.IsActive = true;

            try
            {
                _bleAdvWatcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
                _bleAdvWatcher.Received += BleAdvWatcher_Received;
                _bleAdvWatcher.Start();
                await Task.Delay(4000);
                _bleAdvWatcher.Stop();
                _bleAdvWatcher.Received -= BleAdvWatcher_Received;
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowAsync(ResHelper.Get(_res, "ErrorBleScanTitle"), ex.Message);
            }
            finally
            {
                ScanBleButton.IsEnabled = true;
                AnalyzerProgress.IsActive = false;
                ApplyBleFilter();
                UpdateAnalyzerEmptyStates();
            }
        }

        private void BleAdvWatcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            byte[] macBytes = BitConverter.GetBytes(args.BluetoothAddress);
            string macAddress = string.Join(":", macBytes.Take(6).Reverse().Select(b => b.ToString("X2")));
            string localName = args.Advertisement.LocalName;
            string deviceDisplayName = !string.IsNullOrEmpty(localName)
                ? localName
                : $"{ResHelper.Get(_res, "UnknownDeviceLabel", "[Unknown]")} ({macAddress})";

            lock (_macLock)
            {
                if (!_detectedMacs.Add(macAddress))
                {
                    // update RSSI on existing
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        var existing = _allBle.FirstOrDefault(d => d.MacAddress == macAddress);
                        if (existing != null)
                        {
                            existing.Rssi = args.RawSignalStrengthInDBm;
                            existing.LastSeen = DateTimeOffset.Now;
                        }
                    });
                    return;
                }
            }

            ulong rawAddress = args.BluetoothAddress;
            short rssi = args.RawSignalStrengthInDBm;
            DispatcherQueue.TryEnqueue(() =>
            {
                var model = new BluetoothDeviceModel
                {
                    Name = deviceDisplayName,
                    BluetoothType = "BLE",
                    MacAddress = macAddress,
                    RawBluetoothAddress = rawAddress,
                    CanConnect = false,
                    Rssi = rssi
                };
                _allBle.Add(model);
                if (PassesBleFilter(model))
                    BleDevices.Add(model);
                UpdateAnalyzerEmptyStates();
            });
        }

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyBleFilter();

        private bool PassesBleFilter(BluetoothDeviceModel d)
        {
            string q = FilterBox?.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(q)) return true;
            return d.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                   d.MacAddress.Contains(q, StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyBleFilter()
        {
            string? selectedMac = (BleDeviceListView.SelectedItem as BluetoothDeviceModel)?.MacAddress;
            BleDevices.Clear();
            foreach (var d in _allBle)
            {
                if (PassesBleFilter(d))
                    BleDevices.Add(d);
            }

            if (selectedMac != null)
                BleDeviceListView.SelectedItem = BleDevices.FirstOrDefault(d => d.MacAddress == selectedMac);

            UpdateAnalyzerEmptyStates();
        }

        private void UpdateAnalyzerEmptyStates()
        {
            if (EmptyBleDevicesState == null || EmptyGattState == null) return;

            bool noDevices = BleDevices.Count == 0;
            bool noGatt = GattTree.RootNodes.Count == 0;

            EmptyBleDevicesState.Visibility = noDevices ? Visibility.Visible : Visibility.Collapsed;
            BleDeviceListView.Visibility = noDevices ? Visibility.Collapsed : Visibility.Visible;
            EmptyGattState.Visibility = noGatt ? Visibility.Visible : Visibility.Collapsed;
            GattTree.Visibility = noGatt ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SetCharSelectionVisible(bool hasSelection)
        {
            if (NoCharSelectedState == null || CharActionsPanel == null) return;
            NoCharSelectedState.Visibility = hasSelection ? Visibility.Collapsed : Visibility.Visible;
            CharActionsPanel.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void BleDeviceListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BleDeviceListView.SelectedItem is not BluetoothDeviceModel selectedDevice) return;
            if (selectedDevice.RawBluetoothAddress == 0) return;

            _gattCts?.Cancel();
            _gattCts?.Dispose();
            UnsubscribeAll();
            DisposeGattServices();
            BleServices.Clear();
            GattTree.RootNodes.Clear();
            _allCharacteristics.Clear();
            ClearSelectionUi();

            BleDeviceListView.IsEnabled = false;
            ScanBleButton.IsEnabled = false;
            AnalyzerProgress.IsActive = true;

            _gattCts = new CancellationTokenSource();
            var token = _gattCts.Token;
            int opId = ++_gattOpId;

            try
            {
                _currentBleDevice?.Dispose();
                _currentBleDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(selectedDevice.RawBluetoothAddress);
                if (opId != _gattOpId || token.IsCancellationRequested) return;

                if (_currentBleDevice == null)
                {
                    await DialogHelper.ShowAsync(ResHelper.Get(_res, "ErrorConnBleTitle"), ResHelper.Get(_res, "ErrorNoBleDevice"));
                    return;
                }

                var servicesResult = await _currentBleDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                if (opId != _gattOpId || token.IsCancellationRequested) return;

                if (servicesResult.Status != GattCommunicationStatus.Success)
                {
                    await DialogHelper.ShowAsync(ResHelper.Get(_res, "ErrorGattTitle"), $"Status: {servicesResult.Status}");
                    return;
                }

                foreach (var service in servicesResult.Services)
                {
                    if (opId != _gattOpId || token.IsCancellationRequested) return;
                    var svcModel = new BleServiceModel
                    {
                        Name = DisplayHelpers.ResolveServiceName(service.Uuid),
                        Uuid = service.Uuid.ToString(),
                        Service = service
                    };
                    BleServices.Add(svcModel);

                    var svcNode = new TreeViewNode { Content = svcModel, IsExpanded = false };
                    GattTree.RootNodes.Add(svcNode);

                    try
                    {
                        var charsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                        if (charsResult.Status != GattCommunicationStatus.Success) continue;

                        foreach (var ch in charsResult.Characteristics)
                        {
                            var chModel = CreateCharModel(ch);
                            svcModel.Characteristics.Add(chModel);
                            _allCharacteristics.Add(chModel);
                            svcNode.Children.Add(new TreeViewNode { Content = chModel });

                            // Battery level auto-read
                            if (ch.Uuid.ToString().Contains("2a19", StringComparison.OrdinalIgnoreCase) &&
                                ch.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read))
                            {
                                try
                                {
                                    var br = await ch.ReadValueAsync(BluetoothCacheMode.Uncached);
                                    if (br.Status == GattCommunicationStatus.Success && br.Value.Length > 0)
                                    {
                                        byte[] data = BufferToBytes(br.Value);
                                        selectedDevice.BatteryLevel = data[0];
                                        chModel.Value = $"{data[0]}%";
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }

                _traffic.Append(TrafficDirection.Info, $"Connected GATT: {selectedDevice.Name}");
                RefreshTrafficBox();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (opId == _gattOpId)
                    await DialogHelper.ShowAsync(ResHelper.Get(_res, "ErrorConnBleTitle"), ex.Message);
            }
            finally
            {
                if (opId == _gattOpId)
                {
                    BleDeviceListView.IsEnabled = true;
                    ScanBleButton.IsEnabled = true;
                    AnalyzerProgress.IsActive = false;
                    UpdateAnalyzerEmptyStates();
                }
            }
        }

        private static BleCharacteristicModel CreateCharModel(GattCharacteristic ch)
        {
            var props = ch.CharacteristicProperties;
            return new BleCharacteristicModel
            {
                Name = DisplayHelpers.ResolveCharacteristicName(ch.Uuid),
                Uuid = ch.Uuid.ToString(),
                Properties = props.ToString(),
                Value = "",
                Characteristic = ch,
                CanRead = props.HasFlag(GattCharacteristicProperties.Read),
                CanWrite = props.HasFlag(GattCharacteristicProperties.Write) ||
                           props.HasFlag(GattCharacteristicProperties.WriteWithoutResponse),
                CanNotify = props.HasFlag(GattCharacteristicProperties.Notify) ||
                            props.HasFlag(GattCharacteristicProperties.Indicate)
            };
        }

        private async void GattTree_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
        {
            if (GattTree.SelectedNodes.Count == 0) return;
            var node = GattTree.SelectedNodes[0];
            if (node.Content is not BleCharacteristicModel chModel)
            {
                ClearSelectionUi();
                return;
            }

            _selectedChar = chModel;
            SetCharSelectionVisible(true);
            SelectedCharName.Text = $"{chModel.Name}\n{chModel.Uuid}";
            SelectedCharProps.Text = chModel.Properties;
            ValueText.Text = chModel.Value;
            ReadBtn.IsEnabled = chModel.CanRead;
            WriteBtn.IsEnabled = chModel.CanWrite;
            WriteBox.IsEnabled = chModel.CanWrite;
            SubscribeBtn.IsEnabled = chModel.CanNotify;
            SubscribeBtn.Content = chModel.SubscribeButtonText;

            DescriptorsList.ItemsSource = null;
            chModel.Descriptors.Clear();
            if (chModel.Characteristic != null)
            {
                try
                {
                    var descResult = await chModel.Characteristic.GetDescriptorsAsync(BluetoothCacheMode.Uncached);
                    if (descResult.Status == GattCommunicationStatus.Success)
                    {
                        foreach (var d in descResult.Descriptors)
                        {
                            var dm = new BleDescriptorModel
                            {
                                Name = DisplayHelpers.ResolveDescriptorName(d.Uuid),
                                Uuid = d.Uuid.ToString(),
                                Descriptor = d,
                                Value = ""
                            };
                            try
                            {
                                var read = await d.ReadValueAsync(BluetoothCacheMode.Uncached);
                                if (read.Status == GattCommunicationStatus.Success)
                                    dm.Value = BitConverter.ToString(BufferToBytes(read.Value)).Replace("-", " ");
                            }
                            catch { }
                            chModel.Descriptors.Add(dm);
                        }
                    }
                }
                catch { }
            }
            DescriptorsList.ItemsSource = chModel.Descriptors;
        }

        private void ClearSelectionUi()
        {
            _selectedChar = null;
            SelectedCharName.Text = "";
            SelectedCharProps.Text = "";
            ValueText.Text = "";
            ReadBtn.IsEnabled = false;
            WriteBtn.IsEnabled = false;
            WriteBox.IsEnabled = false;
            SubscribeBtn.IsEnabled = false;
            DescriptorsList.ItemsSource = null;
            SetCharSelectionVisible(false);
        }

        private async void ReadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedChar?.Characteristic == null) return;
            AnalyzerProgress.IsActive = true;
            try
            {
                var result = await _selectedChar.Characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
                if (result.Status == GattCommunicationStatus.Success)
                {
                    byte[] data = BufferToBytes(result.Value);
                    string formatted = FormatBytes(data, _selectedChar.Characteristic.Uuid);
                    _selectedChar.Value = formatted;
                    ValueText.Text = formatted;
                    _traffic.Append(TrafficDirection.Rx, $"{_selectedChar.Uuid}: {formatted}", data.Length);
                    RefreshTrafficBox();
                }
                else
                {
                    ValueText.Text = $"{ResHelper.Get(_res, "CharReadError")} {result.Status}";
                }
            }
            catch (Exception ex)
            {
                ValueText.Text = $"Error: {ex.Message}";
            }
            finally
            {
                AnalyzerProgress.IsActive = false;
            }
        }

        private async void WriteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedChar?.Characteristic == null) return;
            string input = WriteBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(input)) return;

            byte[]? bytes = null;
            int format = WriteFormatCombo.SelectedIndex;
            try
            {
                if (format == 0) // Hex
                {
                    if (!RfcommTerminalSession.TryParseHex(input, out bytes))
                    {
                        await DialogHelper.ShowAsync(ResHelper.Get(_res, "ErrorGattTitle"), ResHelper.Get(_res, "ErrorInvalidHex", "Invalid hex"));
                        return;
                    }
                }
                else if (format == 1) // Text
                {
                    bytes = Encoding.UTF8.GetBytes(input);
                }
                else // Uint8
                {
                    if (!byte.TryParse(input, out byte b))
                    {
                        await DialogHelper.ShowAsync(ResHelper.Get(_res, "ErrorGattTitle"), "Invalid Uint8");
                        return;
                    }
                    bytes = new[] { b };
                }

                var writer = new DataWriter();
                writer.WriteBytes(bytes);
                var status = await _selectedChar.Characteristic.WriteValueAsync(writer.DetachBuffer());
                string hex = BitConverter.ToString(bytes).Replace("-", " ");
                _traffic.Append(TrafficDirection.Tx, $"{_selectedChar.Uuid}: {hex} ({status})", bytes.Length);
                RefreshTrafficBox();
                ValueText.Text = $"Wrote: {hex} [{status}]";
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowAsync(ResHelper.Get(_res, "ErrorGattTitle"), ex.Message);
            }
        }

        private async void SubscribeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedChar?.Characteristic == null) return;
            var ch = _selectedChar.Characteristic;
            string key = ch.Uuid.ToString();

            try
            {
                if (_selectedChar.IsSubscribed)
                {
                    await ch.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.None);
                    if (_notifyHandlers.TryGetValue(key, out var handler))
                    {
                        ch.ValueChanged -= handler;
                        _notifyHandlers.Remove(key);
                    }
                    _selectedChar.IsSubscribed = false;
                    _traffic.Append(TrafficDirection.Info, $"Unsubscribed {key}");
                }
                else
                {
                    var cccd = ch.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate)
                        ? GattClientCharacteristicConfigurationDescriptorValue.Indicate
                        : GattClientCharacteristicConfigurationDescriptorValue.Notify;

                    var status = await ch.WriteClientCharacteristicConfigurationDescriptorAsync(cccd);
                    if (status != GattCommunicationStatus.Success)
                    {
                        await DialogHelper.ShowAsync(ResHelper.Get(_res, "ErrorGattTitle"), status.ToString());
                        return;
                    }

                    TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> handler = (s, args) =>
                    {
                        byte[] data = BufferToBytes(args.CharacteristicValue);
                        string formatted = FormatBytes(data, s.Uuid);
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (_selectedChar?.Uuid == key)
                            {
                                _selectedChar.Value = formatted;
                                ValueText.Text = formatted;
                            }
                            _traffic.Append(TrafficDirection.Rx, $"NOTIFY {key}: {formatted}", data.Length);
                            RefreshTrafficBox();
                        });
                    };
                    ch.ValueChanged += handler;
                    _notifyHandlers[key] = handler;
                    _selectedChar.IsSubscribed = true;
                    _traffic.Append(TrafficDirection.Info, $"Subscribed {key}");
                }

                SubscribeBtn.Content = _selectedChar.SubscribeButtonText;
                RefreshTrafficBox();
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowAsync(ResHelper.Get(_res, "ErrorGattTitle"), ex.Message);
            }
        }

        private async void WriteDescriptor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not BleDescriptorModel dm || dm.Descriptor == null) return;
            try
            {
                var writer = new DataWriter();
                writer.WriteBytes(new byte[] { 0x01, 0x00 }); // enable notify
                var status = await dm.Descriptor.WriteValueAsync(writer.DetachBuffer());
                dm.Value = $"Wrote 01 00 → {status}";
                _traffic.Append(TrafficDirection.Tx, $"DESC {dm.Uuid}: 01 00 ({status})", 2);
                RefreshTrafficBox();
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowAsync(ResHelper.Get(_res, "ErrorGattTitle"), ex.Message);
            }
        }

        private void ClearTraffic_Click(object sender, RoutedEventArgs e)
        {
            _traffic.Clear();
            TrafficLogBox.Text = "";
        }

        private async void ExportTxt_Click(object sender, RoutedEventArgs e) => await _traffic.ExportAsync(csv: false);
        private async void ExportCsv_Click(object sender, RoutedEventArgs e) => await _traffic.ExportAsync(csv: true);

        private async void RunScript_Click(object sender, RoutedEventArgs e)
        {
            string script = ScriptBox.Text ?? "";
            if (string.IsNullOrWhiteSpace(script)) return;

            _scriptCts?.Cancel();
            _scriptCts = new CancellationTokenSource();
            var runner = new BleScriptRunner(
                uuid => _allCharacteristics.FirstOrDefault(c =>
                    c.Characteristic != null &&
                    (c.Characteristic.Uuid == uuid ||
                     c.Uuid.Contains(uuid.ToString()[4..8], StringComparison.OrdinalIgnoreCase)))?.Characteristic,
                msg => DispatcherQueue.TryEnqueue(() =>
                {
                    _traffic.Append(TrafficDirection.Info, msg);
                    RefreshTrafficBox();
                }),
                _traffic);

            try
            {
                AnalyzerProgress.IsActive = true;
                await runner.RunAsync(script, _scriptCts.Token);
                RefreshTrafficBox();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                await DialogHelper.ShowAsync(ResHelper.Get(_res, "ErrorGattTitle"), ex.Message);
            }
            finally
            {
                AnalyzerProgress.IsActive = false;
            }
        }

        private void RefreshTrafficBox()
        {
            TrafficLogBox.Text = _traffic.ToPlainText();
        }

        private void UnsubscribeAll()
        {
            foreach (var ch in _allCharacteristics)
            {
                if (ch.Characteristic == null) continue;
                string key = ch.Characteristic.Uuid.ToString();
                if (_notifyHandlers.TryGetValue(key, out var handler))
                {
                    try { ch.Characteristic.ValueChanged -= handler; } catch { }
                }
                ch.IsSubscribed = false;
            }
            _notifyHandlers.Clear();
        }

        private void DisposeGattServices()
        {
            foreach (var s in BleServices)
            {
                try { s.Service?.Dispose(); } catch { }
                s.Service = null;
            }
        }

        private static byte[] BufferToBytes(IBuffer buffer)
        {
            byte[] data = new byte[buffer.Length];
            using var reader = DataReader.FromBuffer(buffer);
            reader.ReadBytes(data);
            return data;
        }

        private static string FormatBytes(byte[] data, Guid uuid)
        {
            if (data.Length == 0) return "(empty)";
            string hex = string.Join(" ", data.Select(b => b.ToString("X2")));
            if (uuid.ToString().Contains("2a19") && data.Length == 1)
                return $"{data[0]}%";
            try
            {
                string text = Encoding.UTF8.GetString(data).Trim();
                if (!string.IsNullOrEmpty(text) && !text.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t'))
                    return $"{text} [HEX: {hex}]";
            }
            catch { }
            return $"HEX: {hex}";
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            // Pause advertisement scan; keep GATT connection while page is cached.
            if (_bleAdvWatcher != null)
            {
                try { _bleAdvWatcher.Stop(); } catch { }
                _bleAdvWatcher.Received -= BleAdvWatcher_Received;
                _bleAdvWatcher = null;
                ScanBleButton.IsEnabled = true;
                AnalyzerProgress.IsActive = false;
            }
            _scriptCts?.Cancel();
            base.OnNavigatedFrom(e);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _ = RefreshLicenseUiAsync();
            UpdateAnalyzerEmptyStates();
        }

        private async Task RefreshLicenseUiAsync()
        {
            await LicenseManager.CheckLicenseAsync(startTrialIfNeeded: true);
            UpdateUiState();
        }

        private void OnLicenseStatusChanged()
        {
            DispatcherQueue.TryEnqueue(UpdateUiState);
        }

        public void ApplyDemoScene(string sceneFileName)
        {
            // Show analyzer regardless of license for Store shots
            TrialBanner.Visibility = Visibility.Collapsed;
            PurchasePanel.Visibility = Visibility.Collapsed;
            AnalyzerPanel.Visibility = Visibility.Visible;

            BleDevices.Clear();
            _allBle.Clear();
            BleServices.Clear();
            GattTree.RootNodes.Clear();
            _allCharacteristics.Clear();
            _traffic.Clear();

            var device = new BluetoothDeviceModel
            {
                Name = "Medical-MultiSensor-Pro",
                BluetoothType = "BLE",
                MacAddress = "D0:B5:C2:33:44:EF",
                Rssi = -48,
                BatteryLevel = 91,
                Type = ResHelper.Get(_res, "StatusAvailable", "Available"),
                ConnectButtonText = ResHelper.Get(_res, "BtnConnect", "Connect")
            };
            _allBle.Add(device);
            BleDevices.Add(device);

            var heartRate = new BleServiceModel
            {
                Name = "Heart Rate",
                Uuid = "0000180d-0000-1000-8000-00805f9b34fb"
            };
            var hrMeas = new BleCharacteristicModel
            {
                Name = "Heart Rate Measurement",
                Uuid = "00002a37-0000-1000-8000-00805f9b34fb",
                Properties = "Notify, Read",
                Value = "HEX: 06 48",
                CanRead = true,
                CanNotify = true,
                IsSubscribed = true
            };
            var hrLoc = new BleCharacteristicModel
            {
                Name = "Body Sensor Location",
                Uuid = "00002a38-0000-1000-8000-00805f9b34fb",
                Properties = "Read",
                Value = "Chest",
                CanRead = true
            };
            heartRate.Characteristics.Add(hrMeas);
            heartRate.Characteristics.Add(hrLoc);

            var battery = new BleServiceModel
            {
                Name = "Battery Service",
                Uuid = "0000180f-0000-1000-8000-00805f9b34fb"
            };
            var battLevel = new BleCharacteristicModel
            {
                Name = "Battery Level",
                Uuid = "00002a19-0000-1000-8000-00805f9b34fb",
                Properties = "Read, Notify",
                Value = "91 %",
                CanRead = true,
                CanNotify = true
            };
            battery.Characteristics.Add(battLevel);

            var custom = new BleServiceModel
            {
                Name = "Vendor UART",
                Uuid = "6e400001-b5a3-f393-e0a9-e50e24dcca9e"
            };
            var rx = new BleCharacteristicModel
            {
                Name = "RX Characteristic",
                Uuid = "6e400002-b5a3-f393-e0a9-e50e24dcca9e",
                Properties = "Write, WriteWithoutResponse",
                CanWrite = true
            };
            var tx = new BleCharacteristicModel
            {
                Name = "TX Characteristic",
                Uuid = "6e400003-b5a3-f393-e0a9-e50e24dcca9e",
                Properties = "Notify",
                Value = "HEX: 54 45 4D 50 3D 32 33 2E 34",
                CanNotify = true,
                IsSubscribed = true
            };
            custom.Characteristics.Add(rx);
            custom.Characteristics.Add(tx);

            BleServices.Add(heartRate);
            BleServices.Add(battery);
            BleServices.Add(custom);

            foreach (var svc in BleServices)
            {
                var svcNode = new TreeViewNode { Content = svc, IsExpanded = true };
                foreach (var ch in svc.Characteristics)
                {
                    _allCharacteristics.Add(ch);
                    svcNode.Children.Add(new TreeViewNode { Content = ch });
                }
                GattTree.RootNodes.Add(svcNode);
            }

            BleDeviceListView.SelectedItem = device;
            _selectedChar = hrMeas;
            SelectedCharName.Text = hrMeas.Name;
            SelectedCharProps.Text = hrMeas.Properties;
            ValueText.Text = hrMeas.Value;
            ReadBtn.IsEnabled = hrMeas.CanRead;
            WriteBtn.IsEnabled = false;
            WriteBox.IsEnabled = false;
            SubscribeBtn.IsEnabled = hrMeas.CanNotify;
            SubscribeBtn.Content = hrMeas.SubscribeButtonText;
            SetCharSelectionVisible(true);

            _traffic.Append(TrafficDirection.Info, "GATT connected: Medical-MultiSensor-Pro");
            _traffic.Append(TrafficDirection.Tx, "READ 00002a37-…", 0);
            _traffic.Append(TrafficDirection.Rx, "06 48", 2);
            _traffic.Append(TrafficDirection.Info, "NOTIFY subscribed: Heart Rate Measurement");
            _traffic.Append(TrafficDirection.Rx, "06 4A", 2);
            _traffic.Append(TrafficDirection.Tx, "WRITE UART: TEMP?", 5);
            _traffic.Append(TrafficDirection.Rx, "54 45 4D 50 3D 32 33 2E 34", 9);

            if (sceneFileName.StartsWith("06-", StringComparison.Ordinal))
            {
                for (int i = 0; i < 8; i++)
                {
                    _traffic.Append(TrafficDirection.Rx, $"06 {0x48 + i:X2}", 2);
                    _traffic.Append(TrafficDirection.Info, $"Notify #{i + 1} Heart Rate");
                }
            }

            RefreshTrafficBox();
            UpdateAnalyzerEmptyStates();
            AnalyzerProgress.IsActive = false;
            ScanBleButton.IsEnabled = true;
        }

        public void LoadDemoDataForAnalyzerScreenshot()
            => ApplyDemoScene("04-le-analyzer.png");
    }
}
