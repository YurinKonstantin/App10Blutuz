using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace BluetoothManager.Services
{
    public sealed class RfcommServerClient
    {
        public required string Id { get; init; }
        public required StreamSocket Socket { get; init; }
        public DataWriter? Writer { get; set; }
        public DataReader? Reader { get; set; }
    }

    /// <summary>RFCOMM SPP echo server with multi-client support.</summary>
    public sealed class RfcommServerSession : IDisposable
    {
        private RfcommServiceProvider? _provider;
        private StreamSocketListener? _listener;
        private readonly Dictionary<string, RfcommServerClient> _clients = new();
        private readonly object _lock = new();
        private bool _running;
        private bool _disposed;

        public bool IsRunning => _running;
        public int ClientCount
        {
            get { lock (_lock) return _clients.Count; }
        }

        public event Action<string>? StatusChanged;
        public event Action<string, byte[]>? DataReceived;
        public event Action<string>? ClientConnected;
        public event Action<string>? ClientDisconnected;

        public async Task StartAsync()
        {
            if (_running) return;

            _provider = await RfcommServiceProvider.CreateAsync(RfcommServiceId.SerialPort);
            _listener = new StreamSocketListener();
            _listener.ConnectionReceived += OnConnectionReceived;

            await _listener.BindServiceNameAsync(
                _provider.ServiceId.AsString(),
                SocketProtectionLevel.BluetoothEncryptionAllowNullAuthentication);

            _provider.StartAdvertising(_listener);
            _running = true;
            StatusChanged?.Invoke("listening");
        }

        private async void OnConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            string clientId = Guid.NewGuid().ToString("N")[..8];
            var client = new RfcommServerClient
            {
                Id = clientId,
                Socket = args.Socket,
                Writer = new DataWriter(args.Socket.OutputStream),
                Reader = new DataReader(args.Socket.InputStream) { InputStreamOptions = InputStreamOptions.Partial }
            };

            lock (_lock) { _clients[clientId] = client; }
            ClientConnected?.Invoke(clientId);
            StatusChanged?.Invoke("connected");

            try
            {
                while (_running && client.Reader != null)
                {
                    uint bytesRead = await client.Reader.LoadAsync(1024);
                    if (bytesRead == 0) continue;

                    byte[] buffer = new byte[bytesRead];
                    client.Reader.ReadBytes(buffer);
                    DataReceived?.Invoke(clientId, buffer);

                    // Echo
                    try
                    {
                        string text = RfcommTerminalSession.FormatIncomingBytes(buffer);
                        client.Writer?.WriteString($"ECHO: {text}");
                        if (client.Writer != null)
                            await client.Writer.StoreAsync();
                    }
                    catch { }
                }
            }
            catch
            {
                // connection closed
            }
            finally
            {
                RemoveClient(clientId);
            }
        }

        public async Task BroadcastAsync(string message)
        {
            await BroadcastAsync(System.Text.Encoding.UTF8.GetBytes(message));
        }

        public async Task BroadcastAsync(byte[] payload)
        {
            List<RfcommServerClient> snapshot;
            lock (_lock) { snapshot = new List<RfcommServerClient>(_clients.Values); }

            foreach (var client in snapshot)
            {
                try
                {
                    if (client.Writer == null) continue;
                    client.Writer.WriteBytes(payload);
                    await client.Writer.StoreAsync();
                }
                catch { }
            }
        }

        private void RemoveClient(string clientId)
        {
            RfcommServerClient? client;
            lock (_lock)
            {
                if (!_clients.TryGetValue(clientId, out client)) return;
                _clients.Remove(clientId);
            }

            try { client.Writer?.Dispose(); } catch { }
            try { client.Reader?.Dispose(); } catch { }
            try { client.Socket.Dispose(); } catch { }
            ClientDisconnected?.Invoke(clientId);

            lock (_lock)
            {
                if (_clients.Count == 0 && _running)
                    StatusChanged?.Invoke("listening");
            }
        }

        public void Stop()
        {
            _running = false;

            try { _provider?.StopAdvertising(); } catch { }
            _provider = null;

            try { _listener?.Dispose(); } catch { }
            _listener = null;

            List<string> ids;
            lock (_lock) { ids = new List<string>(_clients.Keys); }
            foreach (var id in ids) RemoveClient(id);

            StatusChanged?.Invoke("stopped");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }
    }
}
