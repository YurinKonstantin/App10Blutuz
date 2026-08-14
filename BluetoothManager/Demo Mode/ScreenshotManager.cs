using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace BluetoothManager.Demo_Mode
{
    /// <summary>
    /// Captures the main window for Microsoft Store screenshots.
    /// Saves PNGs under Pictures/BlueIDE-Store/.
    /// </summary>
    public static class ScreenshotManager
    {
        private const int DwmwaExtendedFrameBounds = 9;
        private const int BorderWidth = 8;
        private const int TopBorderWidth = 1;

        public static readonly (string FileName, string Label)[] StoreScenes =
        {
            ("01-devices-terminal.png", "1. Devices + terminal (hero)"),
            ("02-ble-scan.png", "2. BLE scan / RSSI / favorites"),
            ("03-hex-presets.png", "3. Hex TX-RX + presets"),
            ("04-le-analyzer.png", "4. LE Analyzer / GATT"),
            ("05-echo-server.png", "5. Echo server"),
            ("06-traffic-log.png", "6. Traffic log / export"),
        };

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(
            IntPtr hwnd, int dwAttribute, out Rect pvAttribute, int cbAttribute);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public static string GetOutputFolder()
        {
            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            string folder = Path.Combine(pictures, "BlueIDE-Store");
            Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>Captures the main window and saves as PNG. Returns full path or null on failure.
        /// <paramref name="filename"/> may include a language subfolder, e.g. "en-US/01-devices-terminal.png".</summary>
        public static async Task<string?> CaptureWindowAsync(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return null;

            if (!filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                filename += ".png";

            return await Task.Run(() =>
            {
                try
                {
                    if (App.MainWindow == null)
                        return null;

                    IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                    int result = DwmGetWindowAttribute(
                        hwnd, DwmwaExtendedFrameBounds, out Rect rect, Marshal.SizeOf<Rect>());

                    if (result != 0)
                        return null;

                    int fullWidth = rect.Right - rect.Left;
                    int fullHeight = rect.Bottom - rect.Top;
                    if (fullWidth <= 0 || fullHeight <= 0)
                        return null;

                    int cleanWidth = fullWidth - (BorderWidth * 2);
                    int cleanHeight = fullHeight - BorderWidth - TopBorderWidth;
                    if (cleanWidth <= 0 || cleanHeight <= 0)
                        return null;

                    string folder = GetOutputFolder();
                    string finalPath = Path.Combine(folder, filename.Replace('/', Path.DirectorySeparatorChar));
                    string? dir = Path.GetDirectoryName(finalPath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

                    using var srcBitmap = new Bitmap(fullWidth, fullHeight, PixelFormat.Format32bppArgb);
                    using (var gSrc = Graphics.FromImage(srcBitmap))
                    {
                        gSrc.CopyFromScreen(
                            rect.Left, rect.Top, 0, 0,
                            new Size(fullWidth, fullHeight),
                            CopyPixelOperation.SourceCopy);
                    }

                    using var destBitmap = new Bitmap(cleanWidth, cleanHeight, PixelFormat.Format32bppArgb);
                    using (var gDest = Graphics.FromImage(destBitmap))
                    {
                        gDest.DrawImage(
                            srcBitmap,
                            new Rectangle(0, 0, cleanWidth, cleanHeight),
                            new Rectangle(BorderWidth, TopBorderWidth, cleanWidth, cleanHeight),
                            GraphicsUnit.Pixel);
                    }

                    destBitmap.Save(finalPath, ImageFormat.Png);
                    return finalPath;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Screenshot capture failed: {ex.Message}");
                    return null;
                }
            });
        }
    }
}
