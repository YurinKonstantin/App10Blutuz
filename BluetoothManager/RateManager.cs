using System;
using System.Threading.Tasks;
using Windows.Services.Store;
using Windows.System;

namespace BluetoothManager
{
    public static class RateManager
    {
        private const string StoreId = "9N5H75QWBHLL";
        private const string RatedRegistryKey = "App_UserHasRated_v1";
        private const string EngageCountKey = "MeaningfulEngageCount";

        /// <summary>Engage counts at which the soft prompt is shown (second chance if the first was dismissed).</summary>
        private static readonly int[] PromptAtEngageCounts = { 3, 20 };

        /// <summary>
        /// Shows the in-app Store rating dialog. The "rated" flag is set only when a review was actually submitted.
        /// force = Settings button: falls back to the Store review page if the in-app dialog is unavailable.
        /// </summary>
        public static async Task PromptRatingAsync(bool force = false)
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (!force && localSettings.Values.ContainsKey(RatedRegistryKey))
                return;

            try
            {
                StoreRateAndReviewResult result =
                    await LicenseManager.GetStoreContext().RequestRateAndReviewAppAsync();

                switch (result.Status)
                {
                    case StoreRateAndReviewStatus.Succeeded:
                        localSettings.Values[RatedRegistryKey] = true;
                        return;
                    case StoreRateAndReviewStatus.CanceledByUser:
                        return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Rate dialog error: {ex.Message}");
            }

            // In-app dialog unavailable (sideload / old Windows / network) — open the Store page only on explicit request.
            if (!force) return;

            try
            {
                await Launcher.LaunchUriAsync(new Uri($"ms-windows-store://review/?ProductId={StoreId}"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Rate prompt error: {ex.Message}");
            }
        }

        /// <summary>Call after a successful connection. Prompts on the 3rd and 20th engage until the user rates.</summary>
        public static async Task RegisterMeaningfulEngageAsync()
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSettings.Values.ContainsKey(RatedRegistryKey)) return;

            int count = 0;
            if (localSettings.Values.TryGetValue(EngageCountKey, out var v) && v is int i)
                count = i;
            count++;
            localSettings.Values[EngageCountKey] = count;

            if (Array.IndexOf(PromptAtEngageCounts, count) >= 0)
                await PromptRatingAsync();
        }
    }
}
