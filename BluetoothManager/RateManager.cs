using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.System;

namespace BluetoothManager
{
    public static class RateManager
    {
        // Ключ для хранения в реестре приложения, чтобы не надоедать пользователю, если он уже оценил
        private const string RatedRegistryKey = "App_UserHasRated_v1";

        /// <summary>
        /// Вызывает официальное окно оценки приложения в Microsoft Store
        /// </summary>
        public static async Task PromptRatingAsync()
        {
            // 1. Проверяем локальные настройки: если пользователь уже кликал, больше не беспокоим его
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSettings.Values.ContainsKey(RatedRegistryKey))
            {
                return;
            }

            try
            {
                // Запоминаем, что мы показали окно
                localSettings.Values[RatedRegistryKey] = true;

                // 2. Нативный и самый надежный способ для WinUI 3 Desktop:
                // Открываем глубокую системную ссылку (Deep Link) протокола MS Store
                // Она мгновенно выводит аккуратное окно «Оценить приложение» прямо поверх нашей программы
                string storeId = "9N5H75QWBHLL"; // Store ID вашей программы
                var uri = new Uri($"ms-windows-store://review/?ProductId={storeId}");

                await Launcher.LaunchUriAsync(uri);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка вызова окна оценок: {ex.Message}");
            }
        }
    }
}
