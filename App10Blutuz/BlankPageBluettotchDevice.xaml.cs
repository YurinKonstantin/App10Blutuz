using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// Документацию по шаблону элемента "Пустая страница" см. по адресу https://go.microsoft.com/fwlink/?LinkId=234238

namespace App10Blutuz
{
    /// <summary>
    /// Пустая страница, которую можно использовать саму по себе или для перехода внутри фрейма.
    /// </summary>
    public sealed partial class BlankPageBluettotchDevice : Page
    {
        public BlankPageBluettotchDevice()
        {
            this.InitializeComponent();
            
        }
        ResourceLoader resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
        ClassBluetoothDevice device { get; set; }
        CancellationTokenSource cts;
        Task read;
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
          
            
            if (e.Parameter is ClassBluetoothDevice)
            {
                 device = (ClassBluetoothDevice)e.Parameter;
                if (device != null)
                {
                    try
                    {
                        DropRecive.Content = device.formStrReceiv;
                        Dropsend.Content = formStrSend;
                        DeviceText.Text = device.bluetoothDeviced.Name;
                       string s= await device.Connect();
                        cts = new CancellationTokenSource();
                        if (s == "Connect")
                        {
                            sostoinieText.Text = s;
                            terminalText.Text += resourceLoader.GetString("TextYou") + ">> " + device.namea+" "+ s + "\n";
                            read= Task.Run( () =>
                            {
                                ReadString(cts.Token);
                            });
                        }
                        else
                        {


                            if (s == "0")
                            {
                                
                                sostoinieText.Text = resourceLoader.GetString("textNoConect");
                                terminalText.Text += resourceLoader.GetString("TextYou") +">> " + device.namea + " " + resourceLoader.GetString("textNoConect") + "\n";
                            }
                            else
                            {
                                MessageDialog messageDialog = new MessageDialog(s);
                               await messageDialog.ShowAsync();
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        MessageDialog messageDialog = new MessageDialog(ex.Message);
                        await messageDialog.ShowAsync();
                    }
                }
            
                

            }
            else
            {

            }





            base.OnNavigatedTo(e);
        }
        public async void ReadString(CancellationToken canelToken)
        {
            while (!canelToken.IsCancellationRequested)
            {

                var output = await device.ReadString().ConfigureAwait(false);
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    terminalText.Text += device.namea + " >> " + output + "\n";
                });
            }
        }
        private async void AppBarButton_Click(object sender, RoutedEventArgs e)
        {
          string s= await device.write(SendText.Text, formStrSend);
            terminalText.Text += resourceLoader.GetString("TextYou") + ">> " + SendText.Text + "\n";
          
        }
        public string formStrSend = "UTF8";
        private void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            formStrSend = ((MenuFlyoutItem)sender).Tag.ToString();
            Dropsend.Content = ((MenuFlyoutItem)sender).Tag.ToString();
        }

        private void MenuFlyoutItem_Click_1(object sender, RoutedEventArgs e)
        {
            device.formStrReceiv = ((MenuFlyoutItem)sender).Tag.ToString();
            DropRecive.Content = ((MenuFlyoutItem)sender).Tag.ToString();
        }
    }
}
