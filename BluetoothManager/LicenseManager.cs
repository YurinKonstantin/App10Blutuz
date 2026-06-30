using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Services.Store;
using WinRT.Interop;

namespace BluetoothManager
{
    public static class LicenseManager
    {
        private static StoreContext _storeContext;
        // Тестовый или реальный Store ID вашей надстройки из Microsoft Partner Center
        private const string ProStoreId = "9NF72H80049N";
        // Для тестирования до публикации вы можете вручную выставить true, 
        // чтобы проверить работу самого Анализатора без реальной оплаты!
        public static bool IsProUser { get; private set; } = false;

        private static void InitializeContext()
        {
            if (_storeContext == null)
            {
                _storeContext = StoreContext.GetDefault();
                // Важно для WinUI 3: привязываем контекст магазина к главному окну
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

                if (appLicense != null && appLicense.AddOnLicenses.TryGetValue(ProStoreId, out var license))
                {
                    IsProUser = license.IsActive;
                    return;
                }
                IsProUser = false; // По умолчанию
            }
            catch
            {
                IsProUser = false;
            }
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
                    IsProUser = true;
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }


}
