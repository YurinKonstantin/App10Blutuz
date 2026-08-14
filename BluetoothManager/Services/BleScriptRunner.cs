using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace BluetoothManager.Services
{
    /// <summary>
    /// Minimal BLE script DSL for PRO automation.
    /// Lines: WAIT ms | READ uuid | WRITE uuid hex:AA BB | SUBSCRIBE uuid | UNSUBSCRIBE uuid | LOG text
    /// </summary>
    public sealed class BleScriptRunner
    {
        private readonly Func<Guid, GattCharacteristic?> _resolveCharacteristic;
        private readonly Action<string> _log;
        private readonly TrafficLogStore _traffic;

        public BleScriptRunner(
            Func<Guid, GattCharacteristic?> resolveCharacteristic,
            Action<string> log,
            TrafficLogStore traffic)
        {
            _resolveCharacteristic = resolveCharacteristic;
            _log = log;
            _traffic = traffic;
        }

        public async Task RunAsync(string script, CancellationToken token)
        {
            var lines = script.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(l => l.Trim())
                              .Where(l => l.Length > 0 && !l.StartsWith("#"));

            foreach (var line in lines)
            {
                token.ThrowIfCancellationRequested();
                await ExecuteLineAsync(line, token);
            }
        }

        private async Task ExecuteLineAsync(string line, CancellationToken token)
        {
            var parts = SplitArgs(line);
            if (parts.Count == 0) return;
            string cmd = parts[0].ToUpperInvariant();

            switch (cmd)
            {
                case "WAIT":
                    if (parts.Count >= 2 && int.TryParse(parts[1], out int ms))
                        await Task.Delay(ms, token);
                    break;

                case "LOG":
                    string msg = parts.Count > 1 ? string.Join(' ', parts.Skip(1)) : "";
                    _log(msg);
                    _traffic.Append(TrafficDirection.Info, msg);
                    break;

                case "READ":
                    {
                        var ch = Resolve(parts);
                        if (ch == null) { _log("READ: characteristic not found"); break; }
                        var result = await ch.ReadValueAsync(BluetoothCacheMode.Uncached);
                        if (result.Status == GattCommunicationStatus.Success)
                        {
                            byte[] data = BufferToBytes(result.Value);
                            string hex = BitConverter.ToString(data).Replace("-", " ");
                            _log($"READ {ch.Uuid}: {hex}");
                            _traffic.Append(TrafficDirection.Rx, hex, data.Length);
                        }
                        else _log($"READ failed: {result.Status}");
                        break;
                    }

                case "WRITE":
                    {
                        var ch = Resolve(parts);
                        if (ch == null) { _log("WRITE: characteristic not found"); break; }
                        string hexPart = string.Join(' ', parts.Skip(2));
                        if (hexPart.StartsWith("hex:", StringComparison.OrdinalIgnoreCase))
                            hexPart = hexPart[4..].Trim();
                        if (!RfcommTerminalSession.TryParseHex(hexPart, out byte[] bytes))
                        {
                            _log("WRITE: invalid hex");
                            break;
                        }
                        var writer = new DataWriter();
                        writer.WriteBytes(bytes);
                        var status = await ch.WriteValueAsync(writer.DetachBuffer());
                        _log($"WRITE {ch.Uuid}: {status}");
                        _traffic.Append(TrafficDirection.Tx, BitConverter.ToString(bytes).Replace("-", " "), bytes.Length);
                        break;
                    }

                case "SUBSCRIBE":
                    {
                        var ch = Resolve(parts);
                        if (ch == null) { _log("SUBSCRIBE: not found"); break; }
                        var status = await ch.WriteClientCharacteristicConfigurationDescriptorAsync(
                            GattClientCharacteristicConfigurationDescriptorValue.Notify);
                        _log($"SUBSCRIBE {ch.Uuid}: {status}");
                        break;
                    }

                case "UNSUBSCRIBE":
                    {
                        var ch = Resolve(parts);
                        if (ch == null) { _log("UNSUBSCRIBE: not found"); break; }
                        var status = await ch.WriteClientCharacteristicConfigurationDescriptorAsync(
                            GattClientCharacteristicConfigurationDescriptorValue.None);
                        _log($"UNSUBSCRIBE {ch.Uuid}: {status}");
                        break;
                    }

                default:
                    _log($"Unknown command: {cmd}");
                    break;
            }
        }

        private GattCharacteristic? Resolve(List<string> parts)
        {
            if (parts.Count < 2) return null;
            if (!Guid.TryParse(parts[1], out Guid uuid))
            {
                // allow short form like 2a19
                string shortId = parts[1].ToLowerInvariant();
                uuid = Guid.Parse($"0000{shortId.PadLeft(4, '0')}-0000-1000-8000-00805f9b34fb");
            }
            return _resolveCharacteristic(uuid);
        }

        private static List<string> SplitArgs(string line)
        {
            return line.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private static byte[] BufferToBytes(IBuffer buffer)
        {
            byte[] data = new byte[buffer.Length];
            using var reader = DataReader.FromBuffer(buffer);
            reader.ReadBytes(data);
            return data;
        }
    }
}
