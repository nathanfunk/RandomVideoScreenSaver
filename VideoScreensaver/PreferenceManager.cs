using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reflection;
using log4net;
using log4net.Config;

namespace VideoScreensaver {
    // Manages persistent storage for the screensaver.
    // Can't use IsolatedStorage because of a Windows bug (tries to use 8-char filename when screensaver runs on its own, not in preview mode).
    // Can't use Settings for the same reason.
    // Using the registry directly.
    static class PreferenceManager {

        // Logger
        private static readonly ILog logger =
           LogManager.GetLogger(typeof(PreferenceManager));


        public const string BASE_KEY = "VideoScreensaver";
        public const string VIDEO_PREFS_FILE = "Videos";
        public const string VOLUME_PREFS_FILE = "Volume";

        static PreferenceManager()
        {
            BasicConfigurator.Configure();
            logger.Info("Start");
        }

        public static String ReadVideoSettings() {
            string  strPrefsFile = ReadStringValue(VIDEO_PREFS_FILE);
            logger.InfoFormat("VIDEO_PREFS_FILE = {0}", strPrefsFile);
            return strPrefsFile;
        }

        public static void WriteVideoSettings(String videoPath) {
            logger.InfoFormat("Write VIDEO_PREFS_FILE = {0}", videoPath);
            WriteStringValue(VIDEO_PREFS_FILE, videoPath);
        }

        public static double ReadVolumeSetting() {
            string strVolume = ReadStringValue(VOLUME_PREFS_FILE);
            logger.InfoFormat("VOLUME_PREFS_FILE = {0}", strVolume);
            if (strVolume.Length > 0)
            {
                try
                {
                    return Convert.ToDouble(strVolume);
                }
                catch (System.FormatException) { }
                catch (System.OverflowException) { }
            }
            return 0;
        }

        public static void WriteVolumeSetting(double volume) {
            logger.InfoFormat("Write VOLUME_PREFS_FILE = {0}", volume.ToString());
            WriteStringValue(VOLUME_PREFS_FILE, volume.ToString());
        }

        private static Tuple<RegistryKey, RegistryKey> OpenRegistryKey() {
            RegistryKey software = Registry.CurrentUser.CreateSubKey("Software");
            return new Tuple<RegistryKey, RegistryKey>(software.CreateSubKey(BASE_KEY), software);
        }

        private static string ReadStringValue(string valueName) {
            Tuple<RegistryKey, RegistryKey> appKey = OpenRegistryKey();
            try {
                return appKey.Item1.GetValue(valueName, "").ToString();
            }
            finally {
                appKey.Item1.Close();
                appKey.Item2.Close();
            }
        }

        private static void WriteStringValue(string valueName, string valueData) {
            Tuple<RegistryKey, RegistryKey> appKey = OpenRegistryKey();
            try {
                appKey.Item1.SetValue(valueName, valueData);
            }
            finally {
                appKey.Item1.Close();
                appKey.Item2.Close();
            }
        }
    }
}
