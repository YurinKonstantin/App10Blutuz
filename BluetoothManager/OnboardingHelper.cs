using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace BluetoothManager
{
    public static class OnboardingHelper
    {
        public static async Task ShowAsync(bool force = false)
        {
            if (!force && AppSettings.OnboardingCompleted) return;
            if (App.MainWindow?.Content?.XamlRoot == null) return;

            var res = new ResourceLoader();
            var panel = new StackPanel { Spacing = 12, MaxWidth = 420 };

            panel.Children.Add(new TextBlock
            {
                Text = ResHelper.Get(res, "OnboardStep1", "1. Scan for nearby Classic and BLE devices."),
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(new TextBlock
            {
                Text = ResHelper.Get(res, "OnboardStep2", "2. Pair if needed, then Connect for the SPP terminal."),
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(new TextBlock
            {
                Text = ResHelper.Get(res, "OnboardStep3",
                    "3. Enjoy a 14-day PRO trial (Analyzer, echo server, macros), then buy to keep them."),
                TextWrapping = TextWrapping.Wrap
            });

            var dialog = new ContentDialog
            {
                Title = ResHelper.Get(res, "OnboardTitle", "Welcome to BlueIDE"),
                Content = panel,
                PrimaryButtonText = ResHelper.Get(res, "OnboardGotIt", "Got it"),
                XamlRoot = App.MainWindow.Content.XamlRoot
            };

            try
            {
                await dialog.ShowAsync();
            }
            catch (Exception)
            {
                return;
            }
            AppSettings.OnboardingCompleted = true;
        }
    }
}
