using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
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
    public sealed partial class BlankPageScaner : Page
    {
        Windows.Devices.Bluetooth.Rfcomm.RfcommDeviceService _service;
        Windows.Networking.Sockets.StreamSocket _socket;
        List<ClassBluetoothDevice> classBluetoothDevices = new List<ClassBluetoothDevice>();
        public BlankPageScaner()
        {
            this.InitializeComponent();
        }
        public ClassBluetoothDevice ClassBluetoothDeviceSelect { get; set; }
        private async void FindDeviceRfc()
        {
            classBluetoothDevices.Clear();
            var services =
        await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync();
            for (int i = 0; i < services.Count; i++)
            {
             
                
                //if (services[i].Id.Contains("Bluetooth") && (service.ProtectionLevel== SocketProtectionLevel.BluetoothEncryptionWithAuthentication || service.ProtectionLevel == SocketProtectionLevel.BluetoothEncryptionWithAuthentication || SocketProtectionLevel.BluetoothEncryptionAllowNullAuthentication))
                if (services[i].Id.Contains("Bluetooth"))
                {
                    var service = await RfcommDeviceService.FromIdAsync(services[0].Id);
                 //   MessageDialog messageDialog = new MessageDialog(services[i].Properties.ElementAt(2).Key + "\t" + services[i].Properties.ElementAt(2).Value.ToString() +"\n"+ service.ToString());
                  //  await messageDialog.ShowAsync();
                    if (service != null)
                    {
                        classBluetoothDevices.Add(new ClassBluetoothDevice() { namea = services[i].Name, rfcommDeviceService= service });
                    }

                }

            }
            listV.ItemsSource = classBluetoothDevices;
        }
        public async void FindDeviceD()
        {
            PRing.IsActive = true;
            classBluetoothDevices.Clear();
           // var BluetoothDeviceSelector = "System.Devices.DevObjectType:=5 AND System.Devices.Aep.ProtocolId:=\"{E0CBF06C-CD8B-4647-BB8A-263B43F0F974}\"";
          //  var deviceInfoCollection = await DeviceInformation.FindAllAsync(BluetoothDeviceSelector);
         //   foreach (var ff in deviceInfoCollection)
            {

              //  var bluetoothDevice = await BluetoothDevice.FromIdAsync(ff.Id);
             //   await new MessageDialog(ff.Name +"\n"+ bluetoothDevice.Name+"\n"+ bluetoothDevice.HostName.DisplayName).ShowAsync();
            }


            var devices = await DeviceInformation.FindAllAsync();
           
            foreach (var dd in devices)
            {


                var device = dd;
                if (device != null)
                {
                    try
                    {

                        if (device.Id.Contains("Bluetooth"))
                        {


                            var bluetoothDevice = await BluetoothDevice.FromIdAsync(device.Id);

                          //  await new MessageDialog(device.IsEnabled.ToString() + "\n" + bluetoothDevice.ConnectionStatus.ToString() + "\n" + bluetoothDevice.Name + "\n" + bluetoothDevice.DeviceInformation.Pairing.IsPaired.ToString()).ShowAsync();
                            //if (bluetoothDevice.DeviceInformation.IsEnabled)
                            {
                                //   var service = rfcommServices.Services[0];
                                //   await _socket.ConnectAsync(service.ConnectionHostName, service.ConnectionServiceName);
                                classBluetoothDevices.Add(new ClassBluetoothDevice() { namea = dd.Name, bluetoothDeviced = bluetoothDevice });
                            }
                        }
                    }
                    catch(Exception ex)
                    {
                        
                    }
                }
            }
            listV.ItemsSource = classBluetoothDevices;
            PRing.IsActive = false;
        }

        List<string> vs = new List<string>();
   




     


       
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
         
            base.OnNavigatedTo(e);
            FindDeviceD();
        }

        private async void HyperlinkButton_Click(object sender, RoutedEventArgs e)
        {
            //if(ClassBluetoothDeviceSelect!=null)
            // Frame.Navigate(typeof(BlankPageBluettotchDevice), ClassBluetoothDeviceSelect);
            try
            {


                var rfcommServices = await ClassBluetoothDeviceSelect.bluetoothDeviced.GetRfcommServicesForIdAsync(RfcommServiceId.FromUuid(RfcommServiceId.SerialPort.Uuid), BluetoothCacheMode.Uncached);
                if (ClassBluetoothDeviceSelect != null)
                    Frame.Navigate(typeof(BlankPageBluettotchDevice), ClassBluetoothDeviceSelect);
            }
            catch(Exception ex)
            {
                await new MessageDialog(ex.Message).ShowAsync();
            }
        }

        private async void listV_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ClassBluetoothDeviceSelect = (ClassBluetoothDevice)((ListView)sender).SelectedItem;
            if(ClassBluetoothDeviceSelect!=null)
            {
                try
                {
                    
                }
                catch(Exception ex)
                {
                  
                }
            }
        }

        private void AppBarButton_Click(object sender, RoutedEventArgs e)
        {
            FindDeviceD();
        }

        private async  void AppBarButton_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {


                string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };

                DeviceWatcher deviceWatcher =
                            DeviceInformation.CreateWatcher(
                                    BluetoothLEDevice.GetDeviceSelectorFromPairingState(false),
                                    requestedProperties,
                                    DeviceInformationKind.AssociationEndpoint);

                // Register event handlers before starting the watcher.
                // Added, Updated and Removed are required to get all nearby devices
                //  deviceWatcher.Added += DeviceWatcher_Added;
                //   deviceWatcher.Updated += DeviceWatcher_Updated;
                //  deviceWatcher.Removed += DeviceWatcher_Removed;

                // EnumerationCompleted and Stopped are optional to implement.
                //    deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
                //   deviceWatcher.Stopped += DeviceWatcher_Stopped;

                // Start the watcher.
                //deviceWatcher.Start();
                var displayInformation = Windows.Graphics.Display.DisplayInformation.GetForCurrentView();
                double ScreenWidth = 100;
                double ScreenHeight = 100;
                if (displayInformation.CurrentOrientation == Windows.Graphics.Display.DisplayOrientations.Portrait)
                {
                    ScreenWidth = Math.Round(Window.Current.Bounds.Width * displayInformation.RawPixelsPerViewPixel, 0);
                    ScreenHeight = Math.Round(Window.Current.Bounds.Height * displayInformation.RawPixelsPerViewPixel, 0);
                }
                if (displayInformation.CurrentOrientation == Windows.Graphics.Display.DisplayOrientations.Landscape)
                {

                    ScreenWidth = Math.Round(Window.Current.Bounds.Height * displayInformation.RawPixelsPerViewPixel, 0);
                    ScreenHeight = Math.Round(Window.Current.Bounds.Width * displayInformation.RawPixelsPerViewPixel, 0);
                }
                // For devices with software buttons instead hardware
                else if (displayInformation.CurrentOrientation == Windows.Graphics.Display.DisplayOrientations.LandscapeFlipped)
                {
                    ScreenWidth = Math.Round(Window.Current.Bounds.Height * displayInformation.RawPixelsPerViewPixel, 0);
                    ScreenHeight = Math.Round(Window.Current.Bounds.Width * displayInformation.RawPixelsPerViewPixel, 0);
                }
                Rect rect = new Rect(new Point(ScreenHeight, ScreenWidth), new Point(100, 100));
                DevicePicker devicePicker = new DevicePicker();
                //devicePicker.Filter.SupportedDeviceSelectors.Add(BluetoothLEDevice.GetDeviceSelectorFromPairingState(false));
                //   devicePicker.Filter.SupportedDeviceSelectors.Add(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true));
                devicePicker.Filter.SupportedDeviceSelectors.Add(BluetoothDevice.GetDeviceSelectorFromPairingState(false));
                devicePicker.Filter.SupportedDeviceSelectors.Add(BluetoothDevice.GetDeviceSelectorFromPairingState(true));

                var s = await devicePicker.PickSingleDeviceAsync(rect);
                if (s != null)
                {

                    var sd = await s.Pairing.PairAsync();
                    // await new MessageDialog(sd.ToString()+"\n"+ DevicePairingResultStatus.Paired).ShowAsync();
                    if (sd.Status == DevicePairingResultStatus.Paired || sd.Status == DevicePairingResultStatus.AlreadyPaired || sd.Status== DevicePairingResultStatus.AccessDenied)
                    {


                        //var rfcommServices = await ClassBluetoothDeviceSelect.bluetoothDeviced.GetRfcommServicesForIdAsync(RfcommServiceId.FromUuid(RfcommServiceId.SerialPort.Uuid), BluetoothCacheMode.Uncached);
                        var bluetoothDevice = await BluetoothDevice.FromIdAsync(s.Id);
                        Frame.Navigate(typeof(BlankPageBluettotchDevice), new ClassBluetoothDevice() { namea = s.Name, bluetoothDeviced = bluetoothDevice });
                    }
                }
            }
            catch(Exception ex)
            {
                await new MessageDialog(ex.Message).ShowAsync();
            }


        }
    }
}
