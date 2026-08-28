using LEDVision.Camera;
using LEDVision.Model;
using Microsoft.Win32;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web.Management;
using System.Web.ModelBinding;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LEDVision
{
    /// <summary>
    /// Interaction logic for SettingPage.xaml
    /// </summary>
    public partial class SettingPage : Page
    {
        private SettingModel settingModel = new SettingModel();

        public SettingModel SettingModel
        {
            get
            {
                return settingModel;
            }
            set
            {
                if (settingModel != value)
                {
                    settingModel = value;
                    this.DataContext = SettingModel.SettingVal;

                }
            }
        }

        private Model.Model programModel;

        public Model.Model ProgramModel
        {
            get
            {
                return programModel;
            }
            set
            {
                if (programModel != value)
                {
                    programModel = value;
                }
            }
        }

        private DeviceControl device;

        public DeviceControl Device
        {
            get
            {
                return device;
            }

            set
            {
                device = value;
                device.CheckCommunication(systemBoardStatus);
            }
        }
        private MainWindow mainWindow;

        public SettingPage(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;

            InitializeComponent();

           

            GetPortNames();


        }

        private void RefreshComPortBtn_Click(object sender, RoutedEventArgs e)
        {
            GetPortNames();
        }

        private void GetPortNames()
        {
            string[] ports = SerialPort.GetPortNames();
            systemBoardCboBox.Items.Clear();
            foreach (var port in ports)
            {
                systemBoardCboBox.Items.Add(port);
            }
        }

        public void IncreaseTimes()
        {
            SettingModel.SettingVal.CurNum++;
            SaveSettingModel();

        }

        public void ResetTimes()
        {
            SettingModel.SettingVal.CurNum = 0;
            SaveSettingModel();

        }

        public void SaveSettingModel()
        {
            string json = JsonSerializer.Serialize(SettingModel, new JsonSerializerOptions
            {
                WriteIndented = true // Makes JSON output more readable
            });

            File.WriteAllText("setting.json", json);


            mainWindow.autoPage.SettingModel = SettingModel;
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingModel();


        }

        private void ChangeLogDirectoryBtn_Click(object sender, RoutedEventArgs e)
        {
            var folderBrowserDialog = new FolderBrowserDialog()
            {
                Description = "Choose Log Folder",
                ShowNewFolderButton = true,
            };

            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                string logDir = folderBrowserDialog.SelectedPath;
                if (logDir != null)
                {
                    SettingModel.SettingVal.LogDirectory = logDir;
                }
            }
        }

        private void systemBoardCboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SettingModel.SettingVal.ComPort = (sender as System.Windows.Controls.ComboBox).SelectedItem.ToString();
        }

        private void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            Device.comPort = SettingModel.SettingVal.ComPort;
            Device.CheckCommunication(systemBoardStatus);
        }
    }
}