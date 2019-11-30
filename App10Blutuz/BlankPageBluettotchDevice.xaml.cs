using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
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
            Application.Current.Suspending += new SuspendingEventHandler(App_Suspending);

        }
        ResourceLoader resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
        ClassBluetoothDevice device { get; set; }
        CancellationTokenSource cts;
        Task read;
        public List<ClassMessage> classMessages = new List<ClassMessage>();
        
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
                            DateTime dateTime = new DateTime();
                            dateTime = DateTime.Now;
                            classMessages.Add(new ClassMessage() { message= device.namea + " " + s, dateTime = dateTime, tip="system"});
                            terminalText.Text += dateTime.ToString()+">>"+ resourceLoader.GetString("TextYou") + ">> " + device.namea+" "+ s + "\n";
                            read= Task.Run( () =>
                            {
                                ReadString(cts.Token);
                            });
                        }
                        else
                        {


                            if (s == "0")
                            {
                                DateTime dateTime = new DateTime();
                                dateTime = DateTime.Now;
                                sostoinieText.Text = resourceLoader.GetString("textNoConect");
                                classMessages.Add(new ClassMessage() { message = device.namea + " " + resourceLoader.GetString("textNoConect"), dateTime = dateTime, tip = "system" });
                                terminalText.Text += dateTime.ToString() + ">>" + resourceLoader.GetString("TextYou") +">> " + device.namea + " " + resourceLoader.GetString("textNoConect") + "\n";
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
        async void App_Suspending( Object sender,  Windows.ApplicationModel.SuspendingEventArgs e)
        {
         if(v)
            {
               
               await appWindow.CloseAsync();
            }

        }


        public async void ReadString(CancellationToken canelToken)
        {
            while (!canelToken.IsCancellationRequested)
            {

                var output = await device.ReadString().ConfigureAwait(false);
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    DateTime dateTime = new DateTime();
                    dateTime = DateTime.Now;
                    if (blankPagePlot != null && v)
                    {
                        string[] vs = output.Split(new char[] { ' ', ',', ' ', ':' });
                        for(int i=0; i< vs.Length; i++)
                        {
                            if(Int32.TryParse(vs[i], out int j) || Double.TryParse(vs[i], out double t))
                            {
                                blankPagePlot.PlotModel.addPoint(Convert.ToDouble(vs[i]));
                            }
                        }
                       
                    }
                    classMessages.Add(new ClassMessage() { message = device.namea + " " + output, dateTime = dateTime, tip = device.namea });
                    terminalText.Text += dateTime.ToString() + ">>" + device.namea + " >> " + output + "\n";
                });
            }
        }
        private async void AppBarButton_Click(object sender, RoutedEventArgs e)
        {
          string s= await device.write(SendText.Text, formStrSend);
            DateTime dateTime = new DateTime();
            dateTime = DateTime.Now;
            classMessages.Add(new ClassMessage() { message = resourceLoader.GetString("TextYou") + ">> " + SendText.Text, dateTime = dateTime, tip = resourceLoader.GetString("TextYou") });
            terminalText.Text += dateTime.ToString() + ">>" + resourceLoader.GetString("TextYou") + ">> " + SendText.Text + "\n";
          
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

        private void AppBarButton_Click_1(object sender, RoutedEventArgs e)
        {
            classMessages.Clear();
            terminalText.Text = String.Empty;
        }

        private async void AppBarButton_Click_2(object sender, RoutedEventArgs e)
        {
            try
            {


                var folderPicker = new Windows.Storage.Pickers.FolderPicker();
                folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
                folderPicker.FileTypeFilter.Add("*");

                Windows.Storage.StorageFolder folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    Windows.Storage.StorageFile sampleFile =  await folder.CreateFileAsync("ReportBluetooth.txt", Windows.Storage.CreationCollisionOption.GenerateUniqueName);
                    await Windows.Storage.FileIO.WriteTextAsync(sampleFile, terminalText.Text);

                }
                else
                {
                  
                    await new MessageDialog("Operation cancelled.").ShowAsync();
                }
            }
            catch(Exception ex)
            {
                await new MessageDialog(ex.Message).ShowAsync();
            }

        }
        BlankPagePlot blankPagePlot { get; set; }
        AppWindow appWindow;
        bool v = false;
        private async void AppBarButton_Click_3(object sender, RoutedEventArgs e)
        {
            blankPagePlot = new BlankPagePlot();
           
            appWindow = await AppWindow.TryCreateAsync();
      
            ElementCompositionPreview.SetAppWindowContent(appWindow, blankPagePlot);
           
         v=   await appWindow.TryShowAsync();
            if (v)
            {


                blankPagePlot.iniPlot(device.namea);
                blankPagePlot.PlotModel.addSeries();
                Size size = new Size() { Height = 100, Width = 100 };
                appWindow.RequestSize(size);
                appWindow.Closed += delegate
                {
                    v = false;
                    blankPagePlot = null;
                    appWindow = null;
                };

            }
          

        }

        private async void AppBarButton_Click_4(object sender, RoutedEventArgs e)
        {
            try
            {

                await new MessageDialog(v.ToString()).ShowAsync();
                if (blankPagePlot != null && v)
                {


                    blankPagePlot.PlotModel.addPoint(10);
                }
            }
            catch(Exception ex)
            {

            }
        }

        private void AppBarButton_Click_5(object sender, RoutedEventArgs e)
        {
            spl.IsPaneOpen = !spl.IsPaneOpen;
        }
    }
}
