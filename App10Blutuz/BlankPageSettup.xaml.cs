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
                        //Debug.WriteLine($"This is the trial version. Expiration date: {appLicense.ExpirationDate}");
                        textlic.Text = $"You can use this app for {remainingTrialTime} more days before the trial period ends.";
                        StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
                        logger.Log("isTrialEvent");
                        //  textBlock.Text = $"This is the trial version. Expiration date: {appLicense.ExpirationDate}";

                        // Show the features that are available during trial only.
                    }
                    else
                    {
                        textlic.Text = "You have a full license.";
                        StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
                        logger.Log("NotTrialEvent");

                        // Show the features that are available only with a full license.
                    }
                }
                else
                {
                    textlic.Text = "You don't have a license. The trial time can't be determined.";
                    StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
                    logger.Log("NotActiveEvent");
                }

                // Register for the licenced changed event.
                context.OfflineLicensesChanged += context_OfflineLicensesChanged;
            }
            catch(Exception ex)
            {

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

        private async void butBya_Click(object sender, RoutedEventArgs e)
        {try
            {


                StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
                logger.Log("butByaEvent");
                await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?ProductId=9N5H75QWBHLL"));
            }
            catch(Exception ex)
            {

            }
        }
    }
}
