using System;

namespace BluetoothManager
{
    /// <summary>Shares RFCOMM session status with the shell (nav status chip / leave warning).</summary>
    public static class ConnectionStatusHub
    {
        public static bool IsConnected { get; private set; }
        public static string? DeviceName { get; private set; }

        public static event Action? Changed;

        public static void Set(bool connected, string? deviceName = null)
        {
            IsConnected = connected;
            DeviceName = connected ? deviceName : null;
            Changed?.Invoke();
        }

        public static void Clear() => Set(false);
    }
}
