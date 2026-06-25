using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace BluetoothManager.Demo_Mode
{
    //public static class ScreenshotManager
    //{
    //    [DllImport("dwmapi.dll")]
    //    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    //    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    //    [StructLayout(LayoutKind.Sequential)]
    //    private struct RECT
    //    {
    //        public int Left;
    //        public int Top;
    //        public int Right;
    //        public int Bottom;
    //    }

    //    public static async Task CaptureUiAsync(FrameworkElement element, string filename)
    //    {
    //        await Task.Run(() =>
    //        {
    //            try
    //            {
    //                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
    //                int result = DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT rect, Marshal.SizeOf<RECT>());

    //                if (result == 0)
    //                {
    //                    int fullWidth = rect.Right - rect.Left;
    //                    int fullHeight = rect.Bottom - rect.Top;

    //                    if (fullWidth <= 0 || fullHeight <= 0) return;

    //                    // 🎯 ПАРАМЕТРЫ ОБРЕЗКИ (CROP) ДЛЯ WINDOWS 11:
    //                    // Отсекаем по 8 невидимых пикселей тени по бокам и снизу
    //                    int borderWidth = 8;
    //                    int topBorderWidth = 1; // Минимальный отступ сверху для центрирования

    //                    // Вычисляем чистый размер видимого окна BlueIDE
    //                    int cleanWidth = fullWidth - (borderWidth * 2);
    //                    int cleanHeight = fullHeight - borderWidth - topBorderWidth;

    //                    // Создаем временный холст для захвата всего окна с тенями
    //                    using (Bitmap srcBitmap = new Bitmap(fullWidth, fullHeight, PixelFormat.Format32bppArgb))
    //                    {
    //                        using (Graphics gSrc = Graphics.FromImage(srcBitmap))
    //                        {
    //                            gSrc.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(fullWidth, fullHeight), CopyPixelOperation.SourceCopy);
    //                        }

    //                        // Создаем ФИНАЛЬНЫЙ холст точного чистого размера (без полей)
    //                        using (Bitmap destBitmap = new Bitmap(cleanWidth, cleanHeight, PixelFormat.Format32bppArgb))
    //                        {
    //                            using (Graphics gDest = Graphics.FromImage(destBitmap))
    //                            {
    //                                // Копируем в него только центральную часть изображения, отбрасывая края
    //                                gDest.DrawImage(srcBitmap,
    //                                    new Rectangle(0, 0, cleanWidth, cleanHeight),
    //                                    new Rectangle(borderWidth, topBorderWidth, cleanWidth, cleanHeight),
    //                                    GraphicsUnit.Pixel);
    //                            }

    //                            // Сохраняем чистый PNG в папку "Изображения"
    //                            string picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    //                            string finalPath = Path.Combine(picturesPath, filename);

    //                            destBitmap.Save(finalPath, ImageFormat.Png);
    //                        }
    //                    }
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                System.Diagnostics.Debug.WriteLine($"Ошибка обрезки скриншота: {ex.Message}");
    //            }
    //        });
    //    }
    //}
}
