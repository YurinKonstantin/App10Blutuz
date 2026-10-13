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

    public enum PurchaseOutcome
    {
        Succeeded = 0,
        Cancelled = 1,
        Failed = 2
    }

    public static class LicenseManager
    {
        private static StoreContext? _storeContext;
        private const string ProStoreId = "9NF72H80049N";
        public const int TrialDurationDays = 14;

        public static bool IsProUser { get; private set; }
        public static ProAccessKind AccessKind { get; private set; } = ProAccessKind.Free;
        public static int TrialDaysRemaining { get; private set; }
        public static bool IsTrialActive => AccessKind == ProAccessKind.Trial;
        public static bool IsTrialExpired =>
            AccessKind == ProAccessKind.Free && !string.IsNullOrEmpty(AppSettings.TrialStartedUtc);
        public static event Action? StatusChanged;

        private static string? _cachedPrice;
        private static string? _cachedBasePrice;

        private static void InitializeContext()
        {
            if (_storeContext == null)
            {
                _storeContext = StoreContext.GetDefault();
                var windowHandle = WindowNative.GetWindowHandle(App.MainWindow);
                InitializeWithWindow.Initialize(_storeContext, windowHandle);
            }
        }

        public static async Task CheckLicenseAsync(bool startTrialIfNeeded = false)
        {
            try
            {
                InitializeContext();
                StoreAppLicense appLicense = await _storeContext!.GetAppLicenseAsync();

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

            ApplyTrialOrFree(startTrialIfNeeded);
        }

        public static async Task<PurchaseOutcome> RequestPurchaseAsync()
        {
            try
            {
                InitializeContext();
                StorePurchaseResult result = await _storeContext!.RequestPurchaseAsync(ProStoreId);

                switch (result.Status)
                {
                    case StorePurchaseStatus.Succeeded:
                    case StorePurchaseStatus.AlreadyPurchased:
                        ApplyPurchased();
                        return PurchaseOutcome.Succeeded;
                    case StorePurchaseStatus.NotPurchased:
                        // User closed the Store dialog — not an error.
                        return PurchaseOutcome.Cancelled;
                    default:
                        return PurchaseOutcome.Failed;
                }
            }
            catch
            {
                return PurchaseOutcome.Failed;
            }
        }

        /// <summary>
        /// Localized PRO price from the Store (e.g. "$4.99"). BasePrice is set only while a sale is running.
        /// Returns nulls when the Store is unreachable (sideload / offline).
        /// </summary>
        public static async Task<(string? Price, string? BasePrice)> GetProPriceAsync()
        {
            if (_cachedPrice != null)
                return (_cachedPrice, _cachedBasePrice);

            try
            {
                InitializeContext();
                StoreProductQueryResult query = await _storeContext!.GetStoreProductsAsync(
                    new[] { "Durable" }, new[] { ProStoreId });

                if (query.ExtendedError == null &&
                    query.Products.TryGetValue(ProStoreId, out StoreProduct? product) &&
                    product?.Price != null &&
                    !string.IsNullOrEmpty(product.Price.FormattedPrice))
                {
                    _cachedPrice = product.Price.FormattedPrice;
                    _cachedBasePrice = product.Price.IsOnSale &&
                        product.Price.FormattedBasePrice != product.Price.FormattedPrice
                        ? product.Price.FormattedBasePrice
                        : null;
                }
            }
            catch
            {
                // Store unavailable — callers fall back to copy without a price.
            }

            return (_cachedPrice, _cachedBasePrice);
        }

        /// <summary>Shared Store context (window-initialized) for rating and other Store dialogs.</summary>
        public static StoreContext GetStoreContext()
        {
            InitializeContext();
            return _storeContext!;
        }

        private static void ApplyPurchased()
        {
            AccessKind = ProAccessKind.Purchased;
            IsProUser = true;
            TrialDaysRemaining = 0;
            StatusChanged?.Invoke();
        }

        private static void ApplyTrialOrFree(bool startTrialIfNeeded)
        {
            if (startTrialIfNeeded)
                EnsureTrialStarted();

            if (!TryGetTrialStart(out DateTimeOffset start))
            {
                AccessKind = ProAccessKind.Free;
                IsProUser = false;
                TrialDaysRemaining = 0;
                StatusChanged?.Invoke();
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

            StatusChanged?.Invoke();
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
