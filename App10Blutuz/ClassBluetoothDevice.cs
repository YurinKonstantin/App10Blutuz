using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace App10Blutuz
{
   public class ClassBluetoothDevice
    {
       public RfcommDeviceService rfcommDeviceService { get; set; }
        bool sost = false;
        public string formStrReceiv = "UTF8";
        public string name()
        {
            if(rfcommDeviceService != null )
            {
                return rfcommDeviceService.Device.Name;
            }
            
            return String.Empty;
        }
        public string namea { get; set; }
        public BluetoothDevice bluetoothDeviced { get; set; }
        Windows.Devices.Bluetooth.Rfcomm.RfcommDeviceService _service;
        Windows.Networking.Sockets.StreamSocket _socket;
        public async Task<string> Connect()
        {
            var rfcommServices = await bluetoothDeviced.GetRfcommServicesForIdAsync(RfcommServiceId.FromUuid(RfcommServiceId.SerialPort.Uuid), BluetoothCacheMode.Uncached);
            if (rfcommServices.Services.Count > 0)
            {
                try
                {


                    var service = rfcommServices.Services[0];
                    //  await _socket.ConnectAsync(service.ConnectionHostName, service.ConnectionServiceName);
                    _service = service;

                    // Create a socket and connect to the target
                    _socket = new StreamSocket();
                    await _socket.ConnectAsync(
                        _service.ConnectionHostName,
                        _service.ConnectionServiceName,
                        SocketProtectionLevel
                            .BluetoothEncryptionAllowNullAuthentication);
                    sost = true;
                    inputStream = _socket.InputStream.AsStreamForRead();

                    streamReader = new StreamReader(inputStream);
                 
                    return "Connect";
                   


                }
                catch (Exception ex)
                {
                    sost = false;
                    return ex.Message;
                }

            }
            sost = false;
            return "0";
        }
        async Task<string> Disconect(string ss)
        {
            if (sost == true)
            {
                try
                {


                    
                
                    return "Ok";
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }
            else
            {
                return "No";
            }

        }
        public async Task<string> write(string ss, string format)
        {
            if (sost == true)
            {
                try
                {

                    if (format == "UTF8")
                    {
                        var buffer = Encoding.UTF8.GetBytes(ss);
                        var buffer1 = Windows.Security.Cryptography.CryptographicBuffer.CreateFromByteArray(buffer);
                       // var buffer1=  Encoding.UTF8.GetBytes(ss);
                        await _socket.OutputStream.WriteAsync(buffer1);
                    }
                    if (format == "ASCII")
                    {
                        var buffer = Encoding.ASCII.GetBytes(ss);
                        var buffer1 = Windows.Security.Cryptography.CryptographicBuffer.CreateFromByteArray(buffer);
                        // var buffer1=  Encoding.UTF8.GetBytes(ss);
                        await _socket.OutputStream.WriteAsync(buffer1);

                    }
                    if (format == "Unicode")
                    {
                        var buffer = Encoding.Unicode.GetBytes(ss);
                        var buffer1 = Windows.Security.Cryptography.CryptographicBuffer.CreateFromByteArray(buffer);
                        // var buffer1=  Encoding.UTF8.GetBytes(ss);
                        await _socket.OutputStream.WriteAsync(buffer1);

                    }
                    
                 
                    return "Ok";
                }
                catch (Exception ex)
                {
                    //sost = false;
                    return ex.Message;
                }
            }
            else
            {
                return "No";
            }

        }
        Stream inputStream;
        StreamReader streamReader;
        public async Task<string> ReadString()
        {
            string s = String.Empty;
            if (sost == true)
            {
                try
                {

                    s = await streamReader.ReadLineAsync();

                    return s;
                }
                catch(ArgumentNullException )
                {
                    return String.Empty;
                }
                catch (Exception ex)
                {
                   // sost = false;
                    return s+"\n"+ ex.Message;
                }
            }
            else
            {
                return "No";
            }

        }
   

    }
}
