using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace BluetoothManager
{
    /// <summary>
    /// Shared PRO paywall: one dialog with the full PRO feature list and Store price,
    /// one purchase flow (cancel is silent), and "PRO" badges on gated controls.
    /// </summary>
    public static class ProGate
    {
        /// <summary>
        /// Returns true when PRO is available (purchased or trial), otherwise shows the paywall
        /// and returns true only if the user bought PRO from it.
        /// </summary>
        public static async Task<bool> EnsureProAsync(string featureMessage)
        {
            if (LicenseManager.IsProUser) return true;

            await LicenseManager.CheckLicenseAsync(startTrialIfNeeded: true);
            if (LicenseManager.IsProUser) return true;

            if (App.MainWindow?.Content?.XamlRoot == null) return false;

            var (price, basePrice) = await LicenseManager.GetProPriceAsync();
            var dialog = new ContentDialog
            {
                Title = ResHelper.Get("ServerLockTitle", "Available in BlueIDE PRO"),
                Content = BuildPaywallContent(featureMessage, basePrice),
                PrimaryButtonText = BuyButtonText(price),
                CloseButtonText = ResHelper.Get("Cancel", "Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = App.MainWindow.Content.XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return false;
            return await PurchaseAsync();
        }

        /// <summary>Runs the Store purchase. Shows success, or an error only for real failures (not cancel).</summary>
        public static async Task<bool> PurchaseAsync()
        {
            switch (await LicenseManager.RequestPurchaseAsync())
            {
                case PurchaseOutcome.Succeeded:
                    await DialogHelper.ShowAsync(
                        ResHelper.Get("ProPurchaseSuccessTitle", "Success!"),
                        ResHelper.Get("ProPurchaseSuccessContent", "PRO unlocked."));
                    return true;
                case PurchaseOutcome.Cancelled:
                    return false;
                default:
                    await DialogHelper.ShowAsync(
                        ResHelper.Get("ProPurchaseFailTitle", "Purchase Error"),
                        ResHelper.Get("ProPurchaseFailContent", "Failed to complete the purchase."));
                    return false;
            }
        }

        /// <summary>"Buy PRO — $4.99", or plain "Buy PRO" when the Store price is unavailable.</summary>
        public static string BuyButtonText(string? price)
        {
            string plain = ResHelper.Get("BtnBuyPro", "Buy PRO");
            if (string.IsNullOrEmpty(price)) return plain;
            return string.Format(ResHelper.Get("BtnBuyProWithPrice", "Buy PRO — {0}"), price);
        }

        public static async Task<string> GetBuyButtonTextAsync()
        {
            var (price, _) = await LicenseManager.GetProPriceAsync();
            return BuyButtonText(price);
        }

        /// <summary>Sale line ("Sale — was $9.99"), or null when no sale is running.</summary>
        public static async Task<string?> GetSaleTextAsync()
        {
            var (_, basePrice) = await LicenseManager.GetProPriceAsync();
            return string.IsNullOrEmpty(basePrice)
                ? null
                : string.Format(ResHelper.Get("ProPriceSale", "Sale — was {0}"), basePrice);
        }

        private static FrameworkElement BuildPaywallContent(string featureMessage, string? basePrice)
        {
            var panel = new StackPanel { Spacing = 8, MaxWidth = 440 };

            if (!string.IsNullOrEmpty(featureMessage))
                panel.Children.Add(new TextBlock { Text = featureMessage, TextWrapping = TextWrapping.Wrap });

            panel.Children.Add(new TextBlock
            {
                Text = ResHelper.Get("ProFeaturesHeader", "BlueIDE PRO includes:"),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 4, 0, 0)
            });

            foreach (var (key, fallback) in new[]
            {
                ("ProFeatureAnalyzer", "BLE GATT Analyzer: read, write, notify"),
                ("ProFeatureServer", "Bluetooth echo server for loopback tests"),
                ("ProFeatureMacros", "Command macros for repetitive workflows"),
                ("ProFeatureExport", "Traffic export to TXT / CSV")
            })
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "✓  " + ResHelper.Get(key, fallback),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            panel.Children.Add(new TextBlock
            {
                Text = ResHelper.Get("ProOneTimeText.Text", "One-time purchase — no subscription, yours forever."),
                TextWrapping = TextWrapping.Wrap,
                Foreground = GetBrush("TextFillColorSecondaryBrush"),
                Margin = new Thickness(0, 4, 0, 0)
            });

            if (!string.IsNullOrEmpty(basePrice))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = string.Format(ResHelper.Get("ProPriceSale", "Sale — was {0}"), basePrice),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = GetBrush("AccentTextFillColorPrimaryBrush")
                });
            }

            return panel;
        }

        /// <summary>Shows "label [PRO]" until PRO is purchased; plain label afterwards.</summary>
        public static void ApplyBadge(ContentControl control, string label)
        {
            if (LicenseManager.AccessKind == ProAccessKind.Purchased)
            {
                control.Content = label;
                return;
            }

            var pill = new Border
            {
                Background = GetBrush("AccentFillColorDefaultBrush"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 0, 5, 1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "PRO",
                    FontSize = 10,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = GetBrush("TextOnAccentFillColorPrimaryBrush")
                }
            };

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(pill);
            control.Content = row;
        }

        private static Brush? GetBrush(string key)
        {
            try
            {
                return ThemeHelper.GetBrush(key);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
