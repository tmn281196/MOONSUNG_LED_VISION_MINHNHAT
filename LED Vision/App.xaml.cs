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
        protected override void OnStartup(StartupEventArgs e)
        {
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

        protected override void OnExit(ExitEventArgs e)
        {
            // Stop the camera when the application exits
            CameraSetting.Instance.StopCamera();
            base.OnExit(e);
        }
    }
}