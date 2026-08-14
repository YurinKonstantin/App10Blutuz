using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace BluetoothManager.Services
{
    public sealed class RfcommServiceOption
    {
        public required string DisplayName { get; init; }
        public required RfcommDeviceService Service { get; init; }
    }

    public sealed class RfcommTerminalSession : IDisposable
    {
        private BluetoothDevice? _device;
        private RfcommDeviceService? _service;
        private StreamSocket? _socket;
        private DataWriter? _writer;
        private DataReader? _reader;
        private CancellationTokenSource? _listenCts;
        private bool _disposed;
        private string? _deviceId;
        private int _serviceIndex;

        public bool IsConnected => _socket != null && _writer != null;
        public string? ConnectedDeviceName => _device?.Name;
        public string? ConnectedDeviceId => _deviceId;
        public int SelectedServiceIndex => _serviceIndex;

        public event Action<byte[]>? DataReceived;
        public event Action<string>? ConnectionLost;
        public event Action? Disconnected;

        public static async Task<IReadOnlyList<RfcommServiceOption>> DiscoverServicesAsync(string deviceId)
        {
            var device = await BluetoothDevice.FromIdAsync(deviceId);
            if (device == null) return Array.Empty<RfcommServiceOption>();

            var result = await device.GetRfcommServicesAsync(BluetoothCacheMode.Uncached);
            var list = new List<RfcommServiceOption>();
            int i = 0;
            foreach (var svc in result.Services)
            {
                string name = svc.ServiceId.AsString();
                try
                {
                    if (svc.ServiceId == RfcommServiceId.SerialPort)
                        name = "Serial Port (SPP)";
                }
                catch { }

                list.Add(new RfcommServiceOption
                {
                    DisplayName = $"[{i}] {name}",
                    Service = svc
                });
                i++;
            }

            // Caller owns disposing BluetoothDevice if we keep services; dispose device after copying services refs
            // Services are tied to device lifetime — keep device alive via connecting through ConnectAsync instead.
            device.Dispose();
            return list;
        }

        public async Task ConnectAsync(string deviceId, int serviceIndex = 0)
        {
            Disconnect(silent: true);

            _device = await BluetoothDevice.FromIdAsync(deviceId)
                ?? throw new InvalidOperationException("device_access");

            var rfcommServices = await _device.GetRfcommServicesAsync(BluetoothCacheMode.Uncached);
            if (rfcommServices.Services.Count == 0)
                throw new InvalidOperationException("no_spp");

            if (serviceIndex < 0 || serviceIndex >= rfcommServices.Services.Count)
                serviceIndex = 0;

            _serviceIndex = serviceIndex;
            _service = rfcommServices.Services[serviceIndex];
            _deviceId = deviceId;

            _socket = new StreamSocket();
            await _socket.ConnectAsync(_service.ConnectionHostName, _service.ConnectionServiceName);

            _writer = new DataWriter(_socket.OutputStream);
            _reader = new DataReader(_socket.InputStream) { InputStreamOptions = InputStreamOptions.Partial };

            _listenCts = new CancellationTokenSource();
            _ = ListenAsync(_listenCts.Token);
        }

        public async Task SendAsync(byte[] data)
        {
            if (_writer == null) throw new InvalidOperationException("not_connected");
            _writer.WriteBytes(data);
            await _writer.StoreAsync();
        }

        public async Task SendTextAsync(string text, string lineEnding)
        {
            var payload = Encoding.UTF8.GetBytes(text + lineEnding);
            await SendAsync(payload);
        }

        private async Task ListenAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _reader != null)
                {
                    uint bytesRead = await _reader.LoadAsync(1024).AsTask(token);
                    if (bytesRead == 0) continue;

                    byte[] buffer = new byte[bytesRead];
                    _reader.ReadBytes(buffer);
                    DataReceived?.Invoke(buffer);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ConnectionLost?.Invoke(ex.Message);
                Disconnect(silent: false);
            }
        }

        public void Disconnect(bool silent = false)
        {
            try { _listenCts?.Cancel(); } catch { }
            _listenCts?.Dispose();
            _listenCts = null;

            try { _writer?.DetachStream(); } catch { }
            try { _reader?.DetachStream(); } catch { }
            _writer?.Dispose();
            _writer = null;
            _reader?.Dispose();
            _reader = null;

            try { _socket?.Dispose(); } catch { }
            _socket = null;

            try { _service?.Dispose(); } catch { }
            _service = null;

            try { _device?.Dispose(); } catch { }
            _device = null;

            if (!silent) Disconnected?.Invoke();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Disconnect(silent: true);
        }

        public static string FormatIncomingBytes(byte[] data)
        {
            try
            {
                string text = Encoding.UTF8.GetString(data);
                if (!string.IsNullOrEmpty(text) &&
                    !text.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t'))
                {
                    return text;
                }
            }
            catch { }

            return $"[HEX] {string.Join(" ", data.Select(b => b.ToString("X2")))}\n";
        }

        public static bool TryParseHex(string input, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            var cleaned = input.Replace("0x", "", StringComparison.OrdinalIgnoreCase)
                               .Replace(",", " ")
                               .Replace("-", " ");
            var parts = cleaned.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;

            try
            {
                bytes = parts.Select(p => Convert.ToByte(p, 16)).ToArray();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
