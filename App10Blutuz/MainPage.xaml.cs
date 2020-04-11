using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Media.Audio;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Microsoft.Services.Store.Engagement;
using Windows.Services.Store;
using System.Collections.Generic;

// Документацию по шаблону элемента "Пустая страница" см. по адресу https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x419

namespace App10Blutuz
{
    /// <summary>
    /// Пустая страница, которую можно использовать саму по себе или для перехода внутри фрейма.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
          //  Windows.ApplicationModel.Resources.Core.ResourceContext.SetGlobalQualifierValue("Language", "uk");
            this.InitializeComponent();
            FrameworkElement root = (FrameworkElement)Window.Current.Content;
            root.RequestedTheme = AppSettings.Theme;
          


        }
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {


            var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
            
            try
            {
                InitializeLicense();
            }
            catch (Exception ex)
            {

            }

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
                        trial.Visibility = Visibility.Collapsed;
                        // StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
                        //  logger.Log("NotTrialEvent");

                        // Show the features that are available only with a full license.
                    }
                }
                else
                {
                    trial.Visibility = Visibility.Collapsed;
                    textlic.Text = "You don't have a license. The trial time can't be determined.";
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
                        textlic.Text = $"This is the trial version. Expiration date: {appLicense.ExpirationDate}";
                        trial.Visibility = Visibility.Visible;
                        // Show the features that are available during trial only.
                    }
                    else
                    {
                        Debug.WriteLine("rff");
                        trial.Visibility = Visibility.Collapsed;
                        // Show the features that are available only with a full license.
                    }
                }
            }
            catch (Exception ex)
            {
                trial.Visibility = Visibility.Collapsed;
            }
        }
        private async void Button_Click_11(object sender, RoutedEventArgs e)
        {
            try
            {


                // StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
                // logger.Log("butByaEvent");
                await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?ProductId=9N5H75QWBHLL"));
            }
            catch (Exception ex)
            {

            }
        }
        public sealed class BluetoothConnectionHandler
        {
            RfcommServiceProvider provider;
            bool isAdvertising = false;
            StreamSocket socket;
            StreamSocketListener socketListener;
            DataWriter writer;
            DataReader reader;
            Task listeningTask;

            public bool Listening { get; private set; }
          //  Debug.WriteLine("hhhk");
            // I use Actions for transmitting the output and debug output. These are custom classes I created to pack them more conveniently and to be able to just "Trigger" them without checking anything. Replace this with regular Actions and use their invoke methods.
          //  public ActionSingle<string> MessageOutput { get; private set; } = new ActionSingle<string>();
            
          //  public ActionSingle<string> LogOutput { get; private set; } = new ActionSingle<string>();

            // These were in the samples.
            const uint SERVICE_VERSION_ATTRIBUTE_ID = 0x0300;
            const byte SERVICE_VERSION_ATTRIBUTE_TYPE = 0x0a; // UINT32
            const uint SERVICE_VERSION = 200;

            const bool DO_RESPONSE = true;

            public async void StartServer()
            {
                // Initialize the provider for the hosted RFCOMM service.
                provider = await RfcommServiceProvider.CreateAsync(RfcommServiceId.ObexObjectPush);

                // Create a listener for this service and start listening.
                socketListener = new StreamSocketListener();
                socketListener.ConnectionReceived += OnConnectionReceived;
                await socketListener.BindServiceNameAsync(provider.ServiceId.AsString(), SocketProtectionLevel.BluetoothEncryptionAllowNullAuthentication);

                // Set the SDP attributes and start advertising.
                InitializeServiceSdpAttributes(provider);
                provider.StartAdvertising(socketListener);
                isAdvertising = true;
            }

            public void Disconnect()
            {
                Listening = false;
                if (provider != null) { if (isAdvertising) provider.StopAdvertising(); provider = null; } // StopAdvertising relentlessly causes a crash if not advertising.
                if (socketListener != null) { socketListener.Dispose(); socketListener = null; }
                if (writer != null) { writer.DetachStream(); writer.Dispose(); writer = null; }
                if (reader != null) { reader.DetachStream(); reader.Dispose(); reader = null; }
                if (socket != null) { socket.Dispose(); socket = null; }
                if (listeningTask != null) { listeningTask = null; }
            }

            public async void SendMessage(string message)
            {
                // There no need to send a zero length message.
                if (string.IsNullOrEmpty(message)) return;

                // Make sure that the connection is still up and there is a message to send.
                if (socket == null || writer == null) { Debug.WriteLine("Cannot send message: No clients connected."); return; } // "No clients connected, please wait for a client to connect before attempting to send a message."

                uint messageLength = (uint)message.Length;
               // byte[] countBuffer = BitConverter.GetBytes(messageLength);
                byte[] buffer = Encoding.UTF8.GetBytes(message);

                Debug.WriteLine("Sendinghhh: " + message);

                //writer.WriteBytes(countBuffer);
                writer.WriteBytes(buffer);

                await writer.StoreAsync();
            }



            private void InitializeServiceSdpAttributes(RfcommServiceProvider provider)
            {
                DataWriter w = new DataWriter();

                // First write the attribute type.
                w.WriteByte(SERVICE_VERSION_ATTRIBUTE_TYPE);

                // Then write the data.
                w.WriteUInt32(SERVICE_VERSION);

                IBuffer data = w.DetachBuffer();
                provider.SdpRawAttributes.Add(SERVICE_VERSION_ATTRIBUTE_ID, data);
            }

            private void OnConnectionReceived(StreamSocketListener listener, StreamSocketListenerConnectionReceivedEventArgs args)
            {
                provider.StopAdvertising();
                isAdvertising = false;
                provider = null;
                listener.Dispose();
                socket = args.Socket;
                writer = new DataWriter(socket.OutputStream);
                reader = new DataReader(socket.InputStream);
                writer.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                reader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                //StartListening ();
                Debug.WriteLine("Connection established.");
                listeningTask = new Task(() => StartListening());
                listeningTask.Start();
                // Notify connection received.
            }

            private async void StartListening()
            {
                Debug.WriteLine("Starting to listen for input.");
                Listening = true;
                while (Listening)
                {
                    try
                    {
                        // Based on the protocol we've defined, the first uint is the size of the message. [UInt (4)] + [Message (1*n)] - The UInt describes the length of the message.
                        uint readLength = await reader.LoadAsync(sizeof(uint));

                        // Check if the size of the data is expected (otherwise the remote has already terminated the connection).
                        if (!Listening) break;
                        if (readLength < sizeof(uint))
                        {
                            Listening = false;
                            Disconnect();
                            Debug.WriteLine("The connection has been terminated.");
                            break;
                        }

                      // uint messageLength = reader.ReadUInt32();

                       // Debug.WriteLine("messageLength: " + messageLength.ToString());

                        // Load the rest of the message since you already know the length of the data expected.
                      //readLength = await reader.LoadAsync(messageLength);

                        // Check if the size of the data is expected (otherwise the remote has already terminated the connection).
                       // if (!Listening) break;
                      // if (readLength < messageLength)
                        {
                         //   Listening = false;
                         //   Disconnect();
                          //  Debug.WriteLine("The connection has been terminated.");
                          //  break;
                        }

                        string message = reader.ReadString(readLength);
                        Debug.WriteLine("Received messageString: " + message);
                        if (DO_RESPONSE) SendMessage("1");
                        if (message == "Star")
                        {
                            Debug.WriteLine("AudioGraph creation error: " );
                            AudioGraph audioGraph;
                            AudioGraphSettings settings = new AudioGraphSettings(Windows.Media.Render.AudioRenderCategory.Alerts);

                            CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);
                            if (result.Status != AudioGraphCreationStatus.Success)
                            {
                                Debug.WriteLine("AudioGraph creation error: " + result.Status.ToString());
                                //ShowErrorMessage("AudioGraph creation error: " + result.Status.ToString());
                            }

                            audioGraph = result.Graph;
                            ElementSoundPlayer.Volume = 1.0;
                        }
                        if (message == "tSto")
                        {
                            Debug.WriteLine("AudioGraph creation error: ");
                            ElementSoundPlayer.State = ElementSoundPlayerState.Off;
                        }
                    }
                    catch (Exception e)
                    {
                        // If this is an unknown status it means that the error is fatal and retry will likely fail.
                        if (SocketError.GetStatus(e.HResult) == SocketErrorStatus.Unknown)
                        {
                            Listening = false;
                            Disconnect();
                            Debug.WriteLine("Fatal unknown error occurred.");
                            break;
                        }
                    }
                }
                Debug.WriteLine("Stopped to listen for input.");
            }
        }

        private void DiscoverButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            BluetoothConnectionHandler bluetoothConnectionHandler = new BluetoothConnectionHandler();
            bluetoothConnectionHandler.StartServer();
        }
   


        private async void NavView_ItemInvoked(object sender, NavigationViewItemInvokedEventArgs args)
        {
            try
            {


                if (args.IsSettingsInvoked)
                {
                  // GetLicenseInfo();
                     ContentFrame.Navigate(typeof(BlankPageSettup));
                }
                else
                {
                    // find NavigationViewItem with Content that equals InvokedItem

                    var item = ((NavigationView)sender).MenuItems.OfType<NavigationViewItem>().First(x => (string)x.Content == (string)args.InvokedItem);
                    NavView_Navigate(item as NavigationViewItem);
                }
            }
            catch (Exception ex)
            {

            }
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            // you can also add items in code behind
            // NavView.MenuItems.Add(new NavigationViewItemSeparator());
            // NavView.MenuItems.Add(new NavigationViewItem()
            // { Content = "My content", Icon = new SymbolIcon(Symbol.Folder), Tag = "content" });

            // set the initial SelectedItem 
            foreach (NavigationViewItemBase item in NavView.MenuItems)
            {
                if (item is NavigationViewItem && item.Tag.ToString() == "BlankPageScaner")
                {
                    NavView.SelectedItem = item;
                    ContentFrame.Navigate(typeof(BlankPageScaner));
                    // NavView.Header = "IP Scanner";


                    break;
                }
            }

            //  ContentFrame.Navigated += On_Navigated;

            // add keyboard accelerators for backwards navigation
            //  KeyboardAccelerator GoBack = new KeyboardAccelerator();
            // GoBack.Key = VirtualKey.GoBack;
            //  GoBack.Invoked += BackInvoked;
            //  KeyboardAccelerator AltLeft = new KeyboardAccelerator();
            //  AltLeft.Key = VirtualKey.Left;
            //  AltLeft.Invoked += BackInvoked;
            // this.KeyboardAccelerators.Add(GoBack);
            // this.KeyboardAccelerators.Add(AltLeft);
            // ALT routes here
            //   AltLeft.Modifiers = VirtualKeyModifiers.Menu;
            // NavView.IsPaneOpen = false;
            KeyboardAccelerator GoBack = new KeyboardAccelerator();
            GoBack.Key = VirtualKey.GoBack;
            GoBack.Invoked += BackInvoked;
            KeyboardAccelerator AltLeft = new KeyboardAccelerator();
            AltLeft.Key = VirtualKey.Left;
            AltLeft.Invoked += BackInvoked;
            this.KeyboardAccelerators.Add(GoBack);
            this.KeyboardAccelerators.Add(AltLeft);
            // ALT routes here
            AltLeft.Modifiers = VirtualKeyModifiers.Menu;


        }
        private async void NavView_Navigate(NavigationViewItem item)
        {

            switch (item.Tag)
            {

                case "BlankPageScaner":
                    NavView.IsPaneOpen = false;
                    NavView.SelectedItem = null;
                    ContentFrame.Navigate(typeof(BlankPageScaner));
                    break;
                case "BlankPageServer":
                    NavView.IsPaneOpen = false;
                    NavView.SelectedItem = null;
                    ContentFrame.Navigate(typeof(BlankPageServer));
                    break;





            }
        }

        private void ContentFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {

        }

        private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            On_BackRequested();

        }

        private void BackInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            On_BackRequested();
            args.Handled = true;

        }

        private bool On_BackRequested()
        {
            bool navigated = false;

            // don't go back if the nav pane is overlayed
            if (NavView.IsPaneOpen && (NavView.DisplayMode == NavigationViewDisplayMode.Compact || NavView.DisplayMode == NavigationViewDisplayMode.Minimal))
            {
                return false;
            }
            else
            {
                if (ContentFrame.CanGoBack)
                {
                    ContentFrame.GoBack();
                    navigated = true;
                }
            }
            return navigated;
        }
    


}
}
