using System;
using System.Globalization;
using System.Threading.Tasks;
using Windows.Services.Store;
using WinRT.Interop;

namespace BluetoothManager
{
    public enum ProAccessKind
    {
        Free = 0,
        Trial = 1,
        Purchased = 2
    }

    public static class LicenseManager
    {
        private static StoreContext _storeContext;
        private const string ProStoreId = "9NF72H80049N";
        public const int TrialDurationDays = 14;

        public static bool IsProUser { get; private set; }
        public static ProAccessKind AccessKind { get; private set; } = ProAccessKind.Free;
        public static int TrialDaysRemaining { get; private set; }
        public static bool IsTrialActive => AccessKind == ProAccessKind.Trial;
        public static bool IsTrialExpired =>
            AccessKind == ProAccessKind.Free && !string.IsNullOrEmpty(AppSettings.TrialStartedUtc);

        private static void InitializeContext()
        {
            if (_storeContext == null)
            {
                _storeContext = StoreContext.GetDefault();
                var windowHandle = WindowNative.GetWindowHandle(App.MainWindow);
                InitializeWithWindow.Initialize(_storeContext, windowHandle);
            }
        }

        public static async Task CheckLicenseAsync()
        {
            try
            {
                InitializeContext();
                StoreAppLicense appLicense = await _storeContext.GetAppLicenseAsync();

                if (appLicense != null &&
                    appLicense.AddOnLicenses.TryGetValue(ProStoreId, out var license) &&
                    license.IsActive)
                {
                    ApplyPurchased();
                    return;
                }
            }
            catch
            {
                // Offline / Store errors must not wipe an active trial.
            }

            ApplyTrialOrFree();
        }

        public static async Task<bool> RequestPurchaseAsync()
        {
            try
            {
                InitializeContext();
                StorePurchaseResult result = await _storeContext.RequestPurchaseAsync(ProStoreId);

                if (result.Status == StorePurchaseStatus.Succeeded ||
                    result.Status == StorePurchaseStatus.AlreadyPurchased)
                {
                    ApplyPurchased();
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static void ApplyPurchased()
        {
            AccessKind = ProAccessKind.Purchased;
            IsProUser = true;
            TrialDaysRemaining = 0;
        }

        private static void ApplyTrialOrFree()
        {
            EnsureTrialStarted();

            if (!TryGetTrialStart(out DateTimeOffset start))
            {
                AccessKind = ProAccessKind.Free;
                IsProUser = false;
                TrialDaysRemaining = 0;
                return;
            }

            var end = start.AddDays(TrialDurationDays);
            var remaining = (end - DateTimeOffset.UtcNow).TotalDays;

            if (remaining > 0)
            {
                AccessKind = ProAccessKind.Trial;
                IsProUser = true;
                TrialDaysRemaining = Math.Max(1, (int)Math.Ceiling(remaining));
            }
            else
            {
                AccessKind = ProAccessKind.Free;
                IsProUser = false;
                TrialDaysRemaining = 0;
            }
        }

        private static void EnsureTrialStarted()
        {
            if (!string.IsNullOrEmpty(AppSettings.TrialStartedUtc))
                return;

            AppSettings.TrialStartedUtc = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        }

        private static bool TryGetTrialStart(out DateTimeOffset start)
        {
            start = default;
            string raw = AppSettings.TrialStartedUtc;
            if (string.IsNullOrEmpty(raw))
                return false;

            return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out start);
        }
    }
}
