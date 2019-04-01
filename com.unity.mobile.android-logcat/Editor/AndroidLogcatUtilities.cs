#if PLATFORM_ANDROID
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor.Android;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatUtilities
    {
        /// <summary>
        /// Capture the screenshot on the given device.
        /// </summary>
        /// <returns> Return the path to the screenshot on the PC. </returns>
        public static string CaptureScreen(ADB adb, string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                return null;

            try
            {
                const string screenshotPathOnDevice = "/sdcard/screen.png";

                // Capture the screen on the device.
                var cmd = string.Format("-s {0} shell screencap {1}", deviceId, screenshotPathOnDevice);
                AndroidLogcatInternalLog.Log("{0} {1}", adb.GetADBPath(), cmd);

                var errorMsg = "Unable to capture the screen for device ";
                var outputMsg = adb.Run(new[] { cmd }, errorMsg + deviceId);
                if (outputMsg.StartsWith(errorMsg))
                {
                    AndroidLogcatInternalLog.Log(outputMsg);
                    Debug.LogError(outputMsg);
                    return null;
                }

                // Pull screenshot from the device to temp folder.
                var filePath = Path.Combine(Path.GetTempPath(), "screen_" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".png");
                cmd = string.Format("-s {0} pull {1} {2}", deviceId, screenshotPathOnDevice, filePath);
                AndroidLogcatInternalLog.Log("{0} {1}", adb.GetADBPath(), cmd);

                errorMsg = "Unable to pull the screenshot from device ";
                outputMsg = adb.Run(new[] { cmd }, errorMsg + deviceId);
                if (outputMsg.StartsWith(errorMsg))
                {
                    AndroidLogcatInternalLog.Log(outputMsg);
                    Debug.LogError(outputMsg);
                    return null;
                }

                return filePath;
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log("Exception caugth while capturing screen on device {0}. Details\r\n:{1}", deviceId, ex);
                return null;
            }
        }

        /// <summary>
        /// Connect to the device by ip address.
        /// Please refer to https://developer.android.com/studio/command-line/adb#wireless for details.
        /// </summary>
        /// <param name="ip"> The ip address of the device that needs to be connected. Port can be included like 'device_ip_address:port'. Both IPV4 and IPV6 are supported. </param>
        public static void ConnectDevice(ADB adb, string ip)
        {
            var cmd = "connect " + ip;
            AndroidLogcatInternalLog.Log("{0} {1}", adb.GetADBPath(), cmd);

            var errorMsg = "Unable to connect to ";
            var outputMsg = adb.Run(new[] { cmd }, errorMsg + ip);
            if (outputMsg.StartsWith(errorMsg))
            {
                AndroidLogcatInternalLog.Log(outputMsg);
                Debug.LogError(outputMsg);
            }
        }

        /// <summary>
        /// Get the top activity on the given device.
        /// </summary>
        public static bool GetTopActivityInfo(ADB adb, string deviceId, ref string packageName, ref int packagePid)
        {
            if (string.IsNullOrEmpty(deviceId))
                return false;
            try
            {
                var cmd = "-s " + deviceId + " shell \"dumpsys activity\" ";
                AndroidLogcatInternalLog.Log("{0} {1}", adb.GetADBPath(), cmd);
                var output = adb.Run(new[] { cmd }, "Unable to get the top activity.");
                packagePid = AndroidLogcatUtilities.ParseTopActivityPackageInfo(output, out packageName);
                return packagePid != -1;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Return the pid of the given package on the given device.
        /// </summary>
        public static int GetPidFromPackageName(ADB adb, AndroidDevice device, string deviceId, string packageName)
        {
            if (string.IsNullOrEmpty(deviceId))
                return -1;

            try
            {
                var pidofOptionAvailable = Int32.Parse(device.Properties["ro.build.version.sdk"]) >= 24; // pidof option is only available in Android 7 or above.

                string cmd = null;
                if (pidofOptionAvailable)
                    cmd = string.Format("-s {0} shell pidof -s {1}", deviceId, packageName);
                else
                    cmd = string.Format("-s {0} shell ps", deviceId);

                AndroidLogcatInternalLog.Log("{0} {1}", adb.GetADBPath(), cmd);
                var output = adb.Run(new[] { cmd }, "Unable to get the pid of the given packages.");
                if (string.IsNullOrEmpty(output))
                    return -1;

                if (pidofOptionAvailable)
                {
                    AndroidLogcatInternalLog.Log(output);
                    return int.Parse(output);
                }

                return ParsePidInfo(packageName, output);
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log(ex.Message);
                return -1;
            }
        }

        /// <summary>
        /// Return a list of the connected devices.
        /// </summary>
        public static List<string> RetrieveConnectedDeviceIds(ADB adb)
        {
            var deviceIds = new List<string>();

            AndroidLogcatInternalLog.Log("{0} devices", adb.GetADBPath());
            var adbOutput = adb.Run(new[] { "devices" }, "Unable to list connected devices. ");
            foreach (var line in adbOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()))
            {
                AndroidLogcatInternalLog.Log(" " + line);
                if (line.EndsWith("device"))
                {
                    var deviceId = line.Substring(0, line.IndexOf('\t'));
                    deviceIds.Add(deviceId);
                }
            }

            return deviceIds;
        }

        /// <summary>
        /// Return the detail info of the given device.
        /// </summary>
        public static string RetrieveDeviceDetails(AndroidDevice device, string deviceId)
        {
            if (device == null)
                return deviceId;

            var manufacturer = device.Properties["ro.product.manufacturer"];
            var model = device.Properties["ro.product.model"];
            var release = device.Properties["ro.build.version.release"];
            var sdkVersion = device.Properties["ro.build.version.sdk"];

            return string.Format("{0} {1} (version: {2}, sdk: {3}, id: {4})", manufacturer, model, release, sdkVersion, deviceId);
        }

        public static int ParsePidInfo(string packageName, string commandOutput)
        {
            string line = null;
            // Note: Regex is very slow, looping through string is much faster
            using (var sr = new StringReader(commandOutput))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.EndsWith(packageName))
                        break;
                }
            }

            if (string.IsNullOrEmpty(line))
            {
                AndroidLogcatInternalLog.Log("Cannot get process status for '{0}'.", packageName);
                return -1;
            }

            var regex = new Regex(@"\b\d+");
            Match match = regex.Match(line);
            if (!match.Success)
            {
                AndroidLogcatInternalLog.Log("Failed to parse pid of '{0}'from '{1}'.", packageName, line);
                return -1;
            }

            return int.Parse(match.Groups[0].Value);
        }

        public static int ParseTopActivityPackageInfo(string commandOutput, out string packageName)
        {
            packageName = "";
            if (string.IsNullOrEmpty(commandOutput))
                return -1;

            // Note: Regex is very slow, looping through string is much faster
            string line = null;
            using (var sr = new StringReader(commandOutput))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Contains("top-activity"))
                        break;
                }
            }

            if (string.IsNullOrEmpty(line))
            {
                AndroidLogcatInternalLog.Log("Cannot find top activity.");
                return -1;
            }
            AndroidLogcatInternalLog.Log(line);

            var reg = new Regex(@"(?<pid>\d{2,})\:(?<package>[^/]*)");
            var match = reg.Match(line);
            if (!match.Success)
            {
                AndroidLogcatInternalLog.Log("Match '{0}' failed.", line);
                return -1;
            }

            packageName = match.Groups["package"].Value;
            return int.Parse(match.Groups["pid"].Value);
        }
    }

    internal class AndroidLogcatJsonSerialization
    {
        public string m_SelectedDeviceId = String.Empty;

        public AndroidLogcatConsoleWindow.PackageInformation m_SelectedPackage = null;

        public AndroidLogcat.Priority m_SelectedPriority = AndroidLogcat.Priority.Verbose;

        public List<AndroidLogcatConsoleWindow.PackageInformation> m_PackagesForSerialization = null;

        public AndroidLogcatTagsControl m_TagControl = null;
    }
}
#endif