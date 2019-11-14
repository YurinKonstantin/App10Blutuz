using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Audio;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
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
    public sealed partial class BlankPageServer : Page
    {
        public BlankPageServer()
        {
            this.InitializeComponent();
          bluetoothConnectionHandler = new BluetoothConnectionHandler();
            DropRecive.Content = bluetoothConnectionHandler.formStrReceiv;
            Dropsend.Content = formStrSend;
        }
       public string formStrSend = "UTF8";
       
        ResourceLoader resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
        BluetoothConnectionHandler bluetoothConnectionHandler;
        public sealed class BluetoothConnectionHandler
        {
            public string formStrReceiv = "UTF8";
            public Windows.Devices.Bluetooth.Rfcomm.RfcommServiceProvider provider;
          public  bool isAdvertising = false;
           public StreamSocket socket;
            public StreamSocketListener socketListener;
            public DataWriter writer;
           public DataReader reader;
            Task listeningTask;

            public bool Listening { get; set; }
            //  Debug.WriteLine("hhhk");
            // I use Actions for transmitting the output and debug output. These are custom classes I created to pack them more conveniently and to be able to just "Trigger" them without checking anything. Replace this with regular Actions and use their invoke methods.
            //  public ActionSingle<string> MessageOutput { get; private set; } = new ActionSingle<string>();

            //  public ActionSingle<string> LogOutput { get; private set; } = new ActionSingle<string>();

            // These were in the samples.
            const uint SERVICE_VERSION_ATTRIBUTE_ID = 0x0300;
            const byte SERVICE_VERSION_ATTRIBUTE_TYPE = 0x0a; // UINT32
            const uint SERVICE_VERSION = 200;

            const bool DO_RESPONSE = true;

            public async Task<bool> StartServer()
            {
                // Initialize the provider for the hosted RFCOMM service.
                provider = await RfcommServiceProvider.CreateAsync(RfcommServiceId.ObexObjectPush);
                if (provider != null)
                {

                 
                    // Create a listener for this service and start listening.
                    socketListener = new StreamSocketListener();
                    socketListener.ConnectionReceived += OnConnectionReceived;
                 
                    
                    await socketListener.BindServiceNameAsync(provider.ServiceId.AsString(), SocketProtectionLevel.BluetoothEncryptionAllowNullAuthentication);

                    // Set the SDP attributes and start advertising.
                    InitializeServiceSdpAttributes(provider);
                    provider.StartAdvertising(socketListener);
                    isAdvertising = true;
                    return true;
                }
                else
                {
                    await new MessageDialog("No").ShowAsync();
                    return false;
                }
                return false;
              
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

            public async void SendMessage(string message, string format)
            {
                // There no need to send a zero length message.
                if (string.IsNullOrEmpty(message)) return;

                // Make sure that the connection is still up and there is a message to send.
                if (socket == null || writer == null) 
                { Debug.WriteLine("Cannot send message: No clients connected.");
                    Text.Text += ">>Cannot send message: No clients connected."+"\n";
                    return; 
                } // "No clients connected, please wait for a client to connect before attempting to send a message."
                uint messageLength = (uint)message.Length;
                byte[] buffer;
                if (format=="UTF8")
                {
                    buffer = Encoding.UTF8.GetBytes(message);
                    writer.WriteBytes(buffer);

                    await writer.StoreAsync();
                    Text.Text += ">>Send message: " + message+"\n";
                }
                if (format == "ASCII")
                {
                    buffer = Encoding.ASCII.GetBytes(message);
                    writer.WriteBytes(buffer);

                    await writer.StoreAsync();
                    Text.Text += ">>Send message: " + message + "\n";

                }
                if (format == "Unicode")
                {
                    buffer = Encoding.Unicode.GetBytes(message);
                    writer.WriteBytes(buffer);

                    await writer.StoreAsync();
                    Text.Text += ">>Send message: " + message + "\n";

                }
                // byte[] countBuffer = BitConverter.GetBytes(messageLength);
                // buffer = Encoding.UTF8.GetBytes(message);


        

                //writer.WriteBytes(countBuffer);
             
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
            public TextBlock Text;
            private async void OnConnectionReceived(StreamSocketListener listener, StreamSocketListenerConnectionReceivedEventArgs args)
            {
                // provider.StopAdvertising();
                // isAdvertising = false;
                // provider = null;
                //  listener.Dispose();
                
              
                   
               
                socket = args.Socket;
                writer = new DataWriter(socket.OutputStream);
                reader = new DataReader(socket.InputStream);
                inStream = socket.InputStream.AsStreamForRead();
                writer.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                reader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                Debug.WriteLine("Connection device.");
                var remoteDevice = await BluetoothDevice.FromHostNameAsync(socket.Information.RemoteHostName);
                naame = remoteDevice.Name;
                await Text.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { Text.Text += ">>Connection device: "+ remoteDevice.Name+"\n"; });
                Debug.WriteLine("Connection device."+ remoteDevice.Name);
                Debug.WriteLine("Connection established.");
               listeningTask = new Task(() => StartListeringBute());
               listeningTask.Start();
                // Notify connection received.
            }
            string naame = String.Empty;
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
                            //Listening = false;
                           // Disconnect();
                            Debug.WriteLine(readLength.ToString());
                            break;
                        }
                        Debug.WriteLine("The connection has been terminated.");
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

                        // string message = reader.ReadString(readLength);
                        byte[] fb = new byte[readLength];
                        reader.ReadBytes(fb);
                        string message=   Encoding.UTF8.GetString(fb, 0, fb.Length);
                        await Text.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { Text.Text += ">>"+ naame+">>" + message + "\n"; });
                        // if (DO_RESPONSE) SendMessage("1");
                      /*if (message == "Star")
                        {
                            Debug.WriteLine("AudioGraph creation error: ");
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
                        */
                       // if (message == "tSto")
                       // {
                         //   Debug.WriteLine("AudioGraph creation error: ");
                         //   ElementSoundPlayer.State = ElementSoundPlayerState.Off;
                        //}
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
            private Stream inStream = null;
            public async void StartListeringBute()
            {

               
                int bytes;
                while (true)
                {
                    try
                    {

                        byte[] buffer = new byte[1024];
                        bytes = await inStream.ReadAsync(buffer, 0, buffer.Length);
                    

                        if (bytes > 0 && bytes<1025)
                        {


                            string valor = String.Empty;
                            if (formStrReceiv=="UTF8")
                            {
                                valor = ">>" + naame + ">>" + Encoding.UTF8.GetString(buffer, 0, bytes);
                            }
                            if (formStrReceiv == "ASCII")
                            {
                                valor = ">>" + naame + ">>" + Encoding.ASCII.GetString(buffer, 0, bytes);

                            }
                            if (formStrReceiv == "Unicode")
                            {

                                valor = ">>" + naame + ">>" + Encoding.Unicode.GetString(buffer, 0, bytes);
                            }

                            await Text.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                    Text.Text += valor+"\n";
                            });
                               
                            
                        }
                        if (bytes > 1024)
                        {

                            while (bytes > 0)
                            {

                                // List<byte> gg = new List<byte>();

                                //  Result.Text = valor + "\t";
                                // for (int i = 0; i < bytes; i++)
                                {
                                    // gg.Add(buffer[i]);
                                }
                                string valor = Encoding.UTF8.GetString(buffer, 0, bytes);
                                await Text.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                {
                                    Text.Text += valor;
                                });
                                Debug.WriteLine("2S" + bytes.ToString());
                                 bytes = await inStream.ReadAsync(buffer, 0, buffer.Length);
                                Debug.WriteLine("3S" + bytes.ToString());
                                 if (bytes <= 0)
                                {

                                       await Text.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                     {
                                       Text.Text += "\n";
                                  });
                                     Debug.WriteLine("4S" + bytes.ToString());
                                }


                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error");
                    }
                }
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            bluetoothConnectionHandler.Text = terminalText;
           bool b=await bluetoothConnectionHandler.StartServer();
            if(b)
            {
                sostoanie.Text = resourceLoader.GetString("textServerStart");
                elipsSos.Fill = new SolidColorBrush(Windows.UI.Colors.Green);
                BStop.IsEnabled = true;
                BStart.IsEnabled = false;

            }
            //bluetoothConnectionHandler.socketListener.ConnectionReceived += OnConnectionReceived;
             //Task.Run(() => StartListening());
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            bluetoothConnectionHandler.Disconnect();
            sostoanie.Text += resourceLoader.GetString("textServerStop");
            elipsSos.Fill = new SolidColorBrush(Windows.UI.Colors.Red);
            BStop.IsEnabled = false;
            BStart.IsEnabled = true;
        }

        private void AppBarButton_Click(object sender, RoutedEventArgs e)
        {
            if(!String.IsNullOrEmpty(textSend.Text))
            {
                bluetoothConnectionHandler.SendMessage(textSend.Text, formStrSend);
            }
        }

        private void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            formStrSend = ((MenuFlyoutItem)sender).Tag.ToString();
            Dropsend.Content = ((MenuFlyoutItem)sender).Tag.ToString();
        }

        private void MenuFlyoutItem_Click_1(object sender, RoutedEventArgs e)
        {
            bluetoothConnectionHandler.formStrReceiv = ((MenuFlyoutItem)sender).Tag.ToString();
            DropRecive.Content = ((MenuFlyoutItem)sender).Tag.ToString(); 
        }
    }
}
