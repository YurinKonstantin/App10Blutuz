using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Services.Store;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.Services.Store.Engagement;
using Windows.UI.Popups;

// Документацию по шаблону элемента "Пустая страница" см. по адресу https://go.microsoft.com/fwlink/?LinkId=234238

namespace App10Blutuz
{
    /// <summary>
    /// Пустая страница, которую можно использовать саму по себе или для перехода внутри фрейма.
    /// </summary>
    public sealed partial class BlankPageSettup : Page
    {
        public BlankPageSettup()
        {
            this.InitializeComponent();
            FrameworkElement root = (FrameworkElement)Window.Current.Content;
            root.RequestedTheme = AppSettings.Theme;
            InitializeLicense();


        }
        private StoreContext context = null;
        private StoreAppLicense appLicense = null;
      
 
        private async void InitializeLicense()
        {
            try
            {


                if (context == null)
                {
                    context = StoreContext.GetDefault();
                    // If your app is a desktop app that uses the Desktop Bridge, you
                    // may need additional code to configure the StoreContext object.
                    // For more info, see https://aka.ms/storecontext-for-desktop.
                }

                appLicense = await context.GetAppLicenseAsync();

                if (appLicense.IsActive)
                {
                    if (appLicense.IsTrial)
                    {
                        int remainingTrialTime = (appLicense.ExpirationDate - DateTime.Now).Days;
                        trial.Visibility = Visibility.Visible;
                        StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
                        logger.Log("isTrialEvent");
                        StoreProductResult result = await context.GetStoreProductForCurrentAppAsync();
                        if (result.ExtendedError == null)
                        {
                            if (result.Product.Price.IsOnSale)
                            {
                                PurchasePrice.Text = "Sale " + result.Product.Price.FormattedPrice;
                            }
                            else
                            {
                                PurchasePrice.Text = "Price " + result.Product.Price.FormattedBasePrice;
                            }



                        }
                        //Debug.WriteLine($"This is the trial version. Expiration date: {appLicense.ExpirationDate}");

                        textlic.Text = $"You can use this app for {remainingTrialTime} more days before the trial period ends.";
                        // StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
                        // logger.Log("isTrialEvent");
                        //  textBlock.Text = $"This is the trial version. Expiration date: {appLicense.ExpirationDate}";

                        // Show the features that are available during trial only.
                    }
                    else
                    {
                        textlic.Text = "You have a full license.";
                        appLicense = await context.GetAppLicenseAsync();
                        StoreProductResult result = await context.GetStoreProductForCurrentAppAsync();
                        if (result.ExtendedError == null)
                        {

                            if (result.Product.Price.IsOnSale)
                            {
                                PurchasePrice.Text = "Sale " + result.Product.Price.FormattedPrice;
                            }
                            else
                            {
                                PurchasePrice.Text = "Price " + result.Product.Price.FormattedBasePrice;
                            }




                            //PurchasePrice.Text = "Price " + result.Product.Price.FormattedPrice;


                        }
                        StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
                        logger.Log("NotTrialEvent");
                        trial.Visibility = Visibility.Collapsed;
                        // StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
                        //  logger.Log("NotTrialEvent");

                        // Show the features that are available only with a full license.
                    }
                }
                else
                {
                    trial.Visibility = Visibility.Collapsed;
                    StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
                    logger.Log("NotActiveEvent");
                    //textlic.Text = "You don't have a license. The trial time can't be determined.";
                    //StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
                    //  logger.Log("NotActiveEvent");
                }

                // Register for the licenced changed event.
                context.OfflineLicensesChanged += context_OfflineLicensesChanged;
            }
            catch (Exception ex)
            {
                trial.Visibility = Visibility.Collapsed;
                
            }
        }
        private async void context_OfflineLicensesChanged(StoreContext sender, object args)
        {
            // Reload the license.
            try
            {


                appLicense = await context.GetAppLicenseAsync();


                if (appLicense.IsActive)
                {
                    if (appLicense.IsTrial)
                    {
                        Debug.WriteLine($"This is the trial version. Expiration date: {appLicense.ExpirationDate}");
                        //  textBlock.Text = $"This is the trial version. Expiration date: {appLicense.ExpirationDate}";

                        // Show the features that are available during trial only.
                    }
                    else
                    {
                        Debug.WriteLine("rff");
                        // Show the features that are available only with a full license.
                    }
                }
            }
            catch(Exception ex)
            {

            }
        }
        private void SetThemeToggle(ElementTheme theme)
        {
            if (theme == AppSettings.DEFAULTTHEME)
                tglAppTheme.IsOn = false;
            else
                tglAppTheme.IsOn = true;
        }
        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            FrameworkElement window = (FrameworkElement)Window.Current.Content;

            if (((ToggleSwitch)sender).IsOn)
            {
                AppSettings.Theme = AppSettings.NONDEFLTHEME;
                window.RequestedTheme = AppSettings.NONDEFLTHEME;
            }
            else
            {
                AppSettings.Theme = AppSettings.DEFAULTTHEME;
                window.RequestedTheme = AppSettings.DEFAULTTHEME;
            }
        }

        
        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {


                // StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
                // logger.Log("butByaEvent");
                StoreProductResult productResult = await context.GetStoreProductForCurrentAppAsync();
                if (productResult.ExtendedError != null)
                {
                    // The user may be offline or there might be some other server failure


                    return;
                }
                StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
                logger.Log("butByaEvent");
                StorePurchaseResult result = await productResult.Product.RequestPurchaseAsync();
                if (result.ExtendedError != null)
                {
                    MessageDialog messageDialog = new MessageDialog($"Purchase failed: ExtendedError: {result.ExtendedError.Message}");
                    await messageDialog.ShowAsync();

                    return;
                }

                switch (result.Status)
                {
                    case StorePurchaseStatus.AlreadyPurchased:
                        MessageDialog messageDialog = new MessageDialog($"You already bought this app and have a fully-licensed version.");
                        await messageDialog.ShowAsync();

                        break;

                    case StorePurchaseStatus.Succeeded:
                        InitializeLicense();
                        break;

                    case StorePurchaseStatus.NotPurchased:
                        messageDialog = new MessageDialog("Product was not purchased, it may have been canceled.");
                        await messageDialog.ShowAsync();

                        break;

                    case StorePurchaseStatus.NetworkError:
                        messageDialog = new MessageDialog("Product was not purchased due to a Network Error.");
                        await messageDialog.ShowAsync();

                        break;

                    case StorePurchaseStatus.ServerError:
                        messageDialog = new MessageDialog("Product was not purchased due to a Server Error.");
                        await messageDialog.ShowAsync();

                        break;

                    default:
                        messageDialog = new MessageDialog("Product was not purchased due to an Unknown Error.");
                        await messageDialog.ShowAsync();

                        break;
                }
                // await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?ProductId=9PHH2VDQWBG7"));
            }
            catch (Exception ex)
            {

            }
        }
    }
}
