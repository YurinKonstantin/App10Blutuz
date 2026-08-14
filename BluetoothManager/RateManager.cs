using System;
using System.Threading.Tasks;
using Windows.System;

namespace BluetoothManager
{
    public static class RateManager
    {
        private const string RatedRegistryKey = "App_UserHasRated_v1";
        private const string SessionPromptKey = "RatePromptedSession";
        private const string EngageCountKey = "MeaningfulEngageCount";

        /// <summary>
        /// Soft rating prompt: only after meaningful engagement (sessions / PRO), not every packet burst.
        /// </summary>
        public static async Task PromptRatingAsync(bool force = false)
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (!force && localSettings.Values.ContainsKey(RatedRegistryKey))
                return;

            if (!force && localSettings.Values.ContainsKey(SessionPromptKey))
                return;

            try
            {
                string storeId = "9N5H75QWBHLL";
                var uri = new Uri($"ms-windows-store://review/?ProductId={storeId}");

                bool launched = await Launcher.LaunchUriAsync(uri);
                if (launched)
                {
                    localSettings.Values[RatedRegistryKey] = true;
                    localSettings.Values[SessionPromptKey] = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Rate prompt error: {ex.Message}");
            }
        }

        /// <summary>Call after meaningful actions (connect, PRO unlock). Prompts after 3 engages.</summary>
        public static async Task RegisterMeaningfulEngageAsync()
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSettings.Values.ContainsKey(RatedRegistryKey)) return;

            int count = 0;
            if (localSettings.Values.TryGetValue(EngageCountKey, out var v) && v is int i)
                count = i;
            count++;
            localSettings.Values[EngageCountKey] = count;

            if (count >= 3)
                await PromptRatingAsync();
        }
    }
}
