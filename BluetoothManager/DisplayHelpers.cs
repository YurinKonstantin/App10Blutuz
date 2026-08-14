using System;

namespace BluetoothManager
{
    public static class DisplayHelpers
    {
        public static string ResolveServiceName(Guid uuid)
        {
            string uuidStr = uuid.ToString().ToLowerInvariant();
            string? key = null;
            if (uuidStr.Contains("180f")) key = "SvcBattery";
            else if (uuidStr.Contains("180a")) key = "SvcDeviceInfo";
            else if (uuidStr.Contains("180d")) key = "SvcHeartRate";
            else if (uuidStr.Contains("1812")) key = "SvcHid";
            else if (uuidStr.Contains("1800")) key = "SvcGenericAccess";
            else if (uuidStr.Contains("1801")) key = "SvcGenericAttribute";

            if (key != null)
            {
                string localized = ResHelper.Get(key);
                if (!string.IsNullOrEmpty(localized)) return localized;
            }

            return ResHelper.Get("SvcCustom", "Custom Service");
        }

        public static string ResolveCharacteristicName(Guid uuid)
        {
            string uuidStr = uuid.ToString().ToLowerInvariant();
            string? key = null;
            if (uuidStr.Contains("2a19")) key = "CharBatteryLevel";
            else if (uuidStr.Contains("2a29")) key = "CharManufacturer";
            else if (uuidStr.Contains("2a24")) key = "CharModelNumber";
            else if (uuidStr.Contains("2a37")) key = "CharHeartRateMeas";
            else if (uuidStr.Contains("2a00")) key = "CharDeviceName";

            if (key != null)
            {
                string localized = ResHelper.Get(key);
                if (!string.IsNullOrEmpty(localized)) return localized;
            }

            return ResHelper.Get("CharCustom", "Custom Characteristic");
        }

        public static string ResolveDescriptorName(Guid uuid)
        {
            string uuidStr = uuid.ToString().ToLowerInvariant();
            if (uuidStr.Contains("2902"))
                return ResHelper.Get("DescCccd", "Client Characteristic Configuration");
            if (uuidStr.Contains("2901"))
                return ResHelper.Get("DescUserDescription", "Characteristic User Description");
            return ResHelper.Get("DescCustom", "Descriptor");
        }
    }
}
