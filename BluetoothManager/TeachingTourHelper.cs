using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace BluetoothManager
{
    /// <summary>Coordinates first-run TeachingTip tours across pages.</summary>
    public static class TeachingTourHelper
    {
        public static bool ForceRestart { get; set; }

        /// <summary>When true, TeachingTips are not shown (Store screenshot tour).</summary>
        public static bool SuppressTips { get; set; }

        public static bool ShouldShowDevicesTour =>
            !SuppressTips && (ForceRestart || !AppSettings.TeachingTipsCompleted);

        public static bool ShouldShowAnalyzerTip =>
            !SuppressTips && (ForceRestart || !AppSettings.AnalyzerTipShown);

        public static void MarkDevicesTourDone()
        {
            AppSettings.TeachingTipsCompleted = true;
            ForceRestart = false;
        }

        public static void MarkAnalyzerTipDone()
        {
            AppSettings.AnalyzerTipShown = true;
        }

        public static void ResetForReplay()
        {
            AppSettings.TeachingTipsCompleted = false;
            AppSettings.AnalyzerTipShown = false;
            ForceRestart = true;
        }

        public static async Task DelayForLayoutAsync()
        {
            await Task.Delay(450);
        }
    }
}
