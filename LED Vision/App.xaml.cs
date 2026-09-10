using LEDVision.Camera;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;

namespace LEDVision
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Chống mở 2 instance: giữ một Mutex có tên trong suốt phiên chạy. Instance thứ hai thấy Mutex đã tồn tại
        // → đưa cửa sổ của instance đang chạy lên trước rồi thoát.
        private const string SingleInstanceMutexName = "Local\\LEDColorInspection_SingleInstance";
        private static System.Threading.Mutex singleInstanceMutex;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_RESTORE = 9;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;
            singleInstanceMutex = new System.Threading.Mutex(true, SingleInstanceMutexName, out createdNew);
            if (!createdNew)
            {
                BringExistingInstanceToFront();
                Shutdown();
                return;
            }

            base.OnStartup(e);

            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            String macAddr = string.Empty;
            foreach (NetworkInterface adapter in nics)
            {
                if (macAddr == String.Empty)
                {
                    macAddr = adapter.GetPhysicalAddress().ToString();
                }
            }

            // Nhat's MAC address = D8BBC1B21D13
            string[] whiteListMacAddress = { "00D861E3E550", "D4F32D1F99D9" , "D8BBC1B21D13" ,"D843AEC3A404", "089798BE8F26", "00D861E3E550" };

            //bool exists = whiteListMacAddress.Contains(macAddr);

            //if (!exists)
            //{
            //    MessageBox.Show("This Program Cannot be Copied!!!", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            //    App.Current.Shutdown();
            //}
        }

        // Tìm cửa sổ chính của instance đang chạy (cùng tên exe) và đưa lên trước
        private static void BringExistingInstanceToFront()
        {
            try
            {
                var me = System.Diagnostics.Process.GetCurrentProcess();
                foreach (var p in System.Diagnostics.Process.GetProcessesByName(me.ProcessName))
                {
                    if (p.Id == me.Id) continue;
                    IntPtr h = p.MainWindowHandle;
                    if (h != IntPtr.Zero)
                    {
                        ShowWindow(h, SW_RESTORE);
                        SetForegroundWindow(h);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Stop the camera when the application exits
            CameraSetting.Instance.StopCamera();
            try
            {
                if (singleInstanceMutex != null)
                {
                    singleInstanceMutex.ReleaseMutex();
                    singleInstanceMutex.Dispose();
                }
            }
            catch (Exception)
            {
            }
            base.OnExit(e);
        }
    }
}