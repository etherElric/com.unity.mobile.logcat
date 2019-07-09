#if PLATFORM_ANDROID
using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Android;
using System.Text;


namespace Unity.Android.Logcat
{
    interface IAndroidLogcatDevice
    {
        int SDKVersion { get; }

        string Manufacturer { get; }

        string Model { get; }

        string OSVersion { get; }

        string ABI { get; }

        string Id { get; }
    }

    internal class AndroidLogcatDevice : IAndroidLogcatDevice
    {
        private AndroidDevice m_Device;

        public AndroidLogcatDevice(AndroidDevice device)
        {
            m_Device = device;
        }

        public int SDKVersion
        {
            get { return int.Parse(m_Device.Properties["ro.build.version.sdk"]); }
        }

        public string Manufacturer
        {
            get { return m_Device.Properties["ro.product.manufacturer"]; }
        }

        public string Model
        {
            get { return m_Device.Properties["ro.product.model"]; }
        }

        public string OSVersion
        {
            get { return m_Device.Properties["ro.build.version.release"]; }
        }

        public string ABI
        {
            get { return m_Device.Properties["ro.product.cpu.abi"]; }
        }

        public string Id
        {
            get { return m_Device.Id; }
        }
    }
}
#endif
