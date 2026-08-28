using DirectShowLib;
using LEDVision.Camera;
using LEDVision.Model;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static LEDVision.VisionTest;

namespace LEDVision
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public AutoPage autoPage;
        public SettingPage settingPage;
        public VisionPage visionPage;
        public Model.Model programModel;
        public Model.SettingModel settingModel;

        public VisionTest visionTest;
        public DeviceControl device;


        public MainWindow()
        {
            InitializeComponent();

            programModel = new Model.Model();
            //settingModel = new Model.SettingModel();
            device = new DeviceControl();



            settingPage = new SettingPage(this);
            visionPage = new VisionPage(this);
            autoPage = new AutoPage();
            autoPage.SettingPage = settingPage;

            visionTest = new VisionTest(this);

            loadSettingModel();

            autoPageHolder.Content = autoPage;
            settingPageHolder.Content = settingPage;
            visionPageHolder.Content = visionPage;
            autoPageHolder.Visibility = Visibility.Visible;
            autoPageBtn.IsChecked = true;

            settingPage.ProgramModel = programModel;
            visionPage.ProgramModel = programModel;

            settingPage.SettingModel = settingModel;
            visionPage.SettingModel = settingModel;

            autoPage.VisionTest = visionTest;
            visionPage.VisionTest = visionTest;

            autoPage.ProgramModel = programModel;

            autoPage.SettingModel = settingModel;

            settingPage.Device = device;
            visionTest.Device = device;
          

            autoPage.VisionTest.currentPage = "Auto";

            device.OnStartRequest += OnStartRequest;
            device.OnCancleRequest += OnCancelRequest;

        }

        private void OnStartRequest(object sender, EventArgs e)
        {
            if (visionTest.CurrentTestState == TestState.Ready && visionTest.currentPage == "Auto")
            {
                device.TriggerTest = true;
            }
        }

        private void OnCancelRequest(object sender, EventArgs e)
        {
            if (visionTest.CurrentTestState == TestState.Ready && visionTest.currentPage == "Auto")
            {
                //device.Power = false;
                //device.SendControl();

                //Dispatcher.Invoke(new Action(() =>
                //{
                //    //autoPage.readyPopup.Visibility = Visibility.Visible;
                //    autoPage.passPopup.Visibility = Visibility.Hidden;
                //    autoPage.ngPopup.Visibility = Visibility.Hidden;

                //}));
            }
        }


        private void ToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            string btnName = (sender as ToggleButton).Name;

            autoPageBtn.IsChecked = false;
            visionPageBtn.IsChecked = false;
            settingPageBtn.IsChecked = false;

            autoPageHolder.Visibility = Visibility.Collapsed;
            visionPageHolder.Visibility = Visibility.Collapsed;
            settingPageHolder.Visibility = Visibility.Collapsed;

            autoPage.VisionTest.currentPage = "";

            autoPage.DisableObtainFrameTimer();
            visionPage.DisableObtainFrameTimer();

            autoPage.VisionTest.Device.CylinderDown = false;
            autoPage.VisionTest.Device.CylinderUp = false;

            autoPage.VisionTest.Device.SendControl();

            switch (btnName)
            {
                case "autoPageBtn":
                    autoPageHolder.Visibility = Visibility.Visible;
                    autoPage.EnableObtainFrameTimer();
                    autoPageBtn.IsChecked = true;
                    autoPage.VisionTest.currentPage = "Auto";

                    break;

                case "visionPageBtn":
                    visionPageHolder.Visibility = Visibility.Visible;
                    visionPage.EnableObtainFrameTimer();
                    visionPageBtn.IsChecked = true;

              
                    autoPage.VisionTest.currentPage = "Vision";

                    break;

                case "settingPageBtn":
                    settingPageHolder.Visibility = Visibility.Visible;
                    settingPageBtn.IsChecked = true;
                    break;

                default:
                    break;
            }
        }
        // Nút Save ở thanh trái: chuyển tiếp xuống VisionPage (giữ nguyên logic lưu model).
        private void SaveBtn_Click(object sender, RoutedEventArgs e) => visionPage?.SaveModelBtn_Click(sender, e);

        private void loadSettingModel()
        {

            try
            {
                string modelStr = File.ReadAllText("setting.json");
                settingModel = Utility.ConvertFromJson<Model.SettingModel>(modelStr);

            }
            catch
            {
                settingModel = new SettingModel();


            }
            try
            {
                device.comPort = settingModel.SettingVal.ComPort;
         
            }

            catch (Exception)
            {
                MessageBox.Show("Failed to Load Model File! Please Try Again");

            }

            // Đồng bộ cấu hình persist toàn cục từ setting.json
            try
            {
                Camera.SingleLED.PersistEnabled = settingModel.SettingVal.PersistEnabled;
                Camera.SingleLED.PersistFrames = settingModel.SettingVal.PersistFrames;
            }
            catch (Exception)
            {
            }
        }


        private void changeModelBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFile = new OpenFileDialog()
            {
                DefaultExt = ".json",
                Title = "Open model",
            };
            openFile.Filter = "Vision Model File (*.json)|*.json";
            openFile.RestoreDirectory = true;

            if (openFile.ShowDialog() == true)
            {
                string modelStr = File.ReadAllText(openFile.FileName);
                try
                {
                    programModel = Utility.ConvertFromJson<Model.Model>(modelStr);


                    CameraSetting.Instance.cameraSettingValues = programModel.CameraSettingValues;
                    CameraSetting.Instance.loadedCameraSettingValues = programModel.CameraSettingValues.Clone();
                    // Áp thông số camera của model LÊN camera ngay khi mở file (một lần duy nhất)
                    CameraSetting.Instance.SetParammeter(CameraSetting.Instance.cameraSettingValues);

                    settingPage.ProgramModel = programModel;
                    visionPage.ProgramModel = programModel;
                    autoPage.ProgramModel = programModel;

                    autoPage.ModelName = System.IO.Path.GetFileName(openFile.FileName).Replace(".json", "");
                    visionPage.defaultCheckBox.IsChecked = true;

                    autoPage.readyPopup.Visibility = Visibility.Visible;

                }
                catch (Exception)
                {
                    MessageBox.Show("Failed to Load Model File! Please Try Again");
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Left = 0;
            this.Top = 0;
            CameraSetting.Instance.START();
            visionTest.START();


        }
    }
}