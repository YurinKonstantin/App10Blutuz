using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;

namespace BluetoothManager.Services
{
    public sealed class BleAdvertisementHit
    {
        public required string MacAddress { get; init; }
        public required string LocalName { get; init; }
        public short Rssi { get; init; }
        public ulong BluetoothAddress { get; init; }
    }

    /// <summary>
    /// Discovers Classic + BLE (paired, unpaired, advertising) for the Devices list.
    /// </summary>
    public sealed class DeviceWatcherService : IDisposable
    {
        private static readonly string ProtocolFilter =
            "System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\" OR " +
            "System.Devices.Aep.ProtocolId:=\"{bb7bb05e-cc72-411b-a17b-738b10a971d5}\"";

        private static readonly string[] RequestedProperties =
        {
            "System.Devices.Aep.IsConnected",
            "System.Devices.Aep.IsPaired",
            "System.Devices.Aep.IsPresent",
            "System.Devices.Aep.CanPair",
            "System.Devices.Aep.SignalStrength",
            "System.Devices.Aep.DeviceAddress",
            "System.Devices.Aep.Bluetooth.Le.IsConnectable",
            "System.ItemNameDisplay"
        };

        private readonly List<DeviceWatcher> _watchers = new();
        private BluetoothLEAdvertisementWatcher? _leWatcher;
        private int _pendingEnums;
        private bool _disposed;

        public event Action<DeviceInformation>? DeviceAdded;
        public event Action<DeviceInformationUpdate>? DeviceUpdated;
        public event Action<DeviceInformationUpdate>? DeviceRemoved;
        public event Action<BleAdvertisementHit>? BleAdvertisementReceived;
        public event Action? EnumerationCompleted;
        public event Action? Stopped;

        public bool IsRunning { get; private set; }

        public void Start()
        {
            Stop(unsubscribe: true);
            IsRunning = true;
            _pendingEnums = 0;

            // Snapshot paired Classic + LE immediately (headphones etc. often live here).
            _ = SeedPairedAsync();

            // 1) All AssociationEndpoints for Classic + BLE (cached / known) — original working filter.
            // Never honor Removed during scan: inquiry churn drops headphones mid-list.
            AddWatcher(ProtocolFilter, honorRemoved: false);

            // 2) Active inquiry for unpaired Classic/BLE AEPs.
            AddWatcher(
                "(" + ProtocolFilter +
                ") AND System.Devices.Aep.Bluetooth.IssueInquiry:=System.StructuredQueryType.Boolean#True",
                honorRemoved: false);

            // 3) Explicit paired Classic selector (audio headsets often surface here).
            try { AddWatcher(BluetoothDevice.GetDeviceSelectorFromPairingState(true), honorRemoved: false); }
            catch { }

            // 4) Unpaired Classic with inquiry.
            try
            {
                AddWatcher(
                    BluetoothDevice.GetDeviceSelectorFromPairingState(false) +
                    " AND System.Devices.Aep.Bluetooth.IssueInquiry:=System.StructuredQueryType.Boolean#True",
                    honorRemoved: false);
            }
            catch { }

            // 5) Paired + unpaired LE selectors.
            try { AddWatcher(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true), honorRemoved: false); }
            catch { }
            try
            {
                AddWatcher(
                    BluetoothLEDevice.GetDeviceSelectorFromPairingState(false) +
                    " AND System.Devices.Aep.Bluetooth.IssueInquiry:=System.StructuredQueryType.Boolean#True",
                    honorRemoved: false);
            }
            catch { }

            // 6) Raw BLE advertisements.
            _leWatcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };
            _leWatcher.Received += OnLeReceived;
            try { _leWatcher.Start(); } catch { }

            if (_pendingEnums == 0)
                EnumerationCompleted?.Invoke();
        }

        private async Task SeedPairedAsync()
        {
            try
            {
                var classic = await DeviceInformation.FindAllAsync(
                    BluetoothDevice.GetDeviceSelectorFromPairingState(true),
                    RequestedProperties,
                    DeviceInformationKind.AssociationEndpoint);
                foreach (var d in classic)
                    DeviceAdded?.Invoke(d);
            }
            catch { }

            try
            {
                var le = await DeviceInformation.FindAllAsync(
                    BluetoothLEDevice.GetDeviceSelectorFromPairingState(true),
                    RequestedProperties,
                    DeviceInformationKind.AssociationEndpoint);
                foreach (var d in le)
                    DeviceAdded?.Invoke(d);
            }
            catch { }

            // Fallback without Kind — audio headsets often enumerate as Device, not AEP.
            try
            {
                var classicAny = await DeviceInformation.FindAllAsync(
                    BluetoothDevice.GetDeviceSelectorFromPairingState(true));
                foreach (var d in classicAny)
                    DeviceAdded?.Invoke(d);
            }
            catch { }

            try
            {
                var leAny = await DeviceInformation.FindAllAsync(
                    BluetoothLEDevice.GetDeviceSelectorFromPairingState(true));
                foreach (var d in leAny)
                    DeviceAdded?.Invoke(d);
            }
            catch { }

            try
            {
                var allBt = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelector());
                foreach (var d in allBt)
                    DeviceAdded?.Invoke(d);
            }
            catch { }
        }

        private void AddWatcher(string aqs, bool honorRemoved)
        {
            // Prefer AEP; Classic audio selectors often need default Kind=Device.
            if (!TryStartWatcher(aqs, RequestedProperties, DeviceInformationKind.AssociationEndpoint, honorRemoved))
                TryStartWatcher(aqs, RequestedProperties, null, honorRemoved);
        }

        private bool TryStartWatcher(
            string aqs,
            IEnumerable<string> properties,
            DeviceInformationKind? kind,
            bool honorRemoved)
        {
            try
            {
                DeviceWatcher watcher = kind.HasValue
                    ? DeviceInformation.CreateWatcher(aqs, properties, kind.Value)
                    : DeviceInformation.CreateWatcher(aqs, properties);

                watcher.Added += OnAdded;
                watcher.Updated += OnUpdated;
                if (honorRemoved)
                    watcher.Removed += OnRemoved;
                watcher.EnumerationCompleted += OnEnumerationCompleted;
                watcher.Stopped += OnStopped;

                _watchers.Add(watcher);
                _pendingEnums++;
                watcher.Start();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Stop(bool unsubscribe = false)
        {
            IsRunning = false;

            if (_leWatcher != null)
            {
                try { _leWatcher.Stop(); } catch { }
                if (unsubscribe)
                {
                    _leWatcher.Received -= OnLeReceived;
                    _leWatcher = null;
                }
            }

            foreach (var watcher in _watchers)
            {
                try
                {
                    if (watcher.Status == DeviceWatcherStatus.Started ||
                        watcher.Status == DeviceWatcherStatus.EnumerationCompleted)
                    {
                        watcher.Stop();
                    }
                }
                catch { }

                if (unsubscribe)
                {
                    watcher.Added -= OnAdded;
                    watcher.Updated -= OnUpdated;
                    watcher.Removed -= OnRemoved;
                    watcher.EnumerationCompleted -= OnEnumerationCompleted;
                    watcher.Stopped -= OnStopped;
                }
            }

            if (unsubscribe)
            {
                _watchers.Clear();
                _pendingEnums = 0;
            }
        }

        private void OnLeReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            BleAdvertisementReceived?.Invoke(new BleAdvertisementHit
            {
                MacAddress = FormatMac(args.BluetoothAddress),
                LocalName = args.Advertisement.LocalName ?? "",
                Rssi = args.RawSignalStrengthInDBm,
                BluetoothAddress = args.BluetoothAddress
            });
        }

        private static string FormatMac(ulong address)
        {
            byte[] bytes = BitConverter.GetBytes(address);
            return $"{bytes[5]:X2}:{bytes[4]:X2}:{bytes[3]:X2}:{bytes[2]:X2}:{bytes[1]:X2}:{bytes[0]:X2}";
        }

        private void OnAdded(DeviceWatcher sender, DeviceInformation device) => DeviceAdded?.Invoke(device);
        private void OnUpdated(DeviceWatcher sender, DeviceInformationUpdate update) => DeviceUpdated?.Invoke(update);

        private void OnRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
        {
            // During an active scan inquiry churn causes false removals (e.g. headphones flicker out).
            if (IsRunning) return;
            DeviceRemoved?.Invoke(update);
        }

        private void OnEnumerationCompleted(DeviceWatcher sender, object args)
        {
            if (_pendingEnums > 0) _pendingEnums--;
            if (_pendingEnums <= 0)
                EnumerationCompleted?.Invoke();
        }

        private void OnStopped(DeviceWatcher sender, object args)
        {
            if (!IsRunning && _watchers.TrueForAll(w =>
                    w.Status == DeviceWatcherStatus.Stopped ||
                    w.Status == DeviceWatcherStatus.Aborted ||
                    w.Status == DeviceWatcherStatus.Created))
            {
                Stopped?.Invoke();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop(unsubscribe: true);
        }
    }
}
