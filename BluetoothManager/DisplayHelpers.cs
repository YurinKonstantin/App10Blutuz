using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BluetoothManager
{
    public static class DisplayHelpers
    {
        // Парсер имен стандартных сервисов по спецификации Bluetooth SIG
        public static string ResolveServiceName(Guid uuid)
        {
            string uuidStr = uuid.ToString().ToLower();
            if (uuidStr.Contains("180f")) return "Battery Service (Уровень батареи)";
            if (uuidStr.Contains("180a")) return "Device Information (О софте/железе)";
            if (uuidStr.Contains("180d")) return "Heart Rate (Пульсометр)";
            if (uuidStr.Contains("1812")) return "Human Interface Device (Мышь/Клавиатура)";

            return "Custom Service (Пользовательский)";
        }

        // Парсер имен характеристик
        public static string ResolveCharacteristicName(Guid uuid)
        {
            string uuidStr = uuid.ToString().ToLower();
            if (uuidStr.Contains("2a19")) return "Battery Level (%)";
            if (uuidStr.Contains("2a29")) return "Manufacturer Name String";
            if (uuidStr.Contains("2a24")) return "Model Number String";
            if (uuidStr.Contains("2a37")) return "Heart Rate Measurement";

            return "Custom Characteristic";
        }
    }
}
