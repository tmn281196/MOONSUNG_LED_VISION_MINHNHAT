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
        // Đường dẫn file model đang mở (để Save ghi đè không cần hỏi). Rỗng = chưa mở / chưa lưu file nào.
        public string CurrentModelPath { get; private set; } = "";

        // Save / Save As ở thanh top: chuyển tiếp xuống VisionPage (nơi giữ model đang chỉnh)
        private void SaveBtn_Click(object sender, RoutedEventArgs e) => visionPage?.SaveModelBtn_Click(sender, e);
        private void SaveAsBtn_Click(object sender, RoutedEventArgs e) => visionPage?.SaveModelAsBtn_Click(sender, e);

        // JSON của model tại lần mở / lưu gần nhất. Khác với JSON hiện tại = đã sửa → hỏi lưu khi tắt.
        private string lastSavedModelJson = null;

        public void MarkModelSaved()
        {
            lastSavedModelJson = visionPage?.GetModelJson();
        }

        public bool IsModelDirty()
        {
            if (visionPage == null) return false;
            if (lastSavedModelJson == null) return false;
            return visionPage.GetModelJson() != lastSavedModelJson;
        }

        // Tắt chương trình: model có sửa mà chưa lưu thì hỏi. Không sửa thì tắt luôn.
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (IsModelDirty())
            {
                var r = MessageBox.Show("The model has unsaved changes." + Environment.NewLine + "Save before exit?", "LED COLOR INSPECTION",
                                        MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Yes);
                if (r == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                if (r == MessageBoxResult.Yes)
                {
                    visionPage.SaveModelBtn_Click(this, new RoutedEventArgs());
                    if (IsModelDirty())
                    {
                        e.Cancel = true; // người dùng hủy hộp thoại lưu → không tắt
                        return;
                    }
                }
            }
            base.OnClosing(e);
        }

        // Nháy chữ "Saved" cạnh tên model 1.5 s sau khi lưu xong
        public void FlashSaved()
        {
            try
            {
                savedText.Visibility = Visibility.Visible;
                var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                t.Tick += (s, a) => { t.Stop(); savedText.Visibility = Visibility.Collapsed; };
                t.Start();
            }
            catch (Exception)
            {
            }
        }

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
                Camera.SingleLED.PersistMs = settingModel.SettingVal.PersistMs;
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
                LoadModelFromFile(openFile.FileName, true);
            }
        }

        // Nạp model từ file: dùng cho nút Open và cho tự mở model gần nhất lúc khởi động
        public bool LoadModelFromFile(string path, bool showError)
        {
            try
            {
                string modelStr = File.ReadAllText(path);
                programModel = Utility.ConvertFromJson<Model.Model>(modelStr);

                CameraSetting.Instance.cameraSettingValues = programModel.CameraSettingValues;
                CameraSetting.Instance.loadedCameraSettingValues = programModel.CameraSettingValues.Clone();
                // Áp thông số camera của model LÊN camera ngay khi mở file (camera chưa mở thì OnCameraOpened sẽ áp sau)
                CameraSetting.Instance.SetParammeter(CameraSetting.Instance.cameraSettingValues);

                settingPage.ProgramModel = programModel;
                visionPage.ProgramModel = programModel;
                autoPage.ProgramModel = programModel;

                autoPage.ModelName = System.IO.Path.GetFileNameWithoutExtension(path);
                SetModelName(autoPage.ModelName);
                visionPage.defaultCheckBox.IsChecked = true;

                autoPage.readyPopup.Visibility = Visibility.Visible;

                RememberLastModel(path);
                MarkModelSaved();
                return true;
            }
            catch (Exception)
            {
                if (showError)
                {
                    MessageBox.Show("Failed to Load Model File! Please Try Again");
                }
                return false;
            }
        }

        // Ghi nhớ file model vừa mở / lưu vào setting.json để lần sau tự mở
        public void RememberLastModel(string path)
        {
            try
            {
                if (settingModel == null || settingModel.SettingVal == null) return;
                CurrentModelPath = path;
                if (settingModel.SettingVal.LastModelPath == path) return;
                settingModel.SettingVal.LastModelPath = path;
                settingPage?.SaveSettingModel();
            }
            catch (Exception)
            {
            }
        }

        // Tự mở model gần nhất khi khởi động (nếu file còn tồn tại)
        private void TryOpenLastModel()
        {
            try
            {
                string path = settingModel?.SettingVal?.LastModelPath;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
                LoadModelFromFile(path, false);
            }
            catch (Exception)
            {
            }
        }

        // ====== Thanh tiêu đề tự làm (WindowStyle=None): cửa sổ luôn phóng to, không kéo / không resize ======
        private void FitToWorkArea()
        {
            var wa = SystemParameters.WorkArea;
            this.WindowState = System.Windows.WindowState.Normal;
            this.Left = wa.Left;
            this.Top = wa.Top;
            this.Width = wa.Width;
            this.Height = wa.Height;
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = System.Windows.WindowState.Minimized;
        }

        // Khôi phục từ thanh taskbar → về lại đúng vùng làm việc
        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (this.WindowState == System.Windows.WindowState.Maximized) FitToWorkArea();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Cửa sổ không viền: phủ đúng vùng làm việc (không đè taskbar), không kéo / không resize
            FitToWorkArea();
            CameraSetting.Instance.START();
            visionTest.START();

            // Tự mở file model gần nhất
            TryOpenLastModel();
            // Chưa có model nào → lấy trạng thái hiện tại làm mốc "chưa sửa"
            if (lastSavedModelJson == null) MarkModelSaved();

            // Đổi màu chữ 2 nút Reconnect theo trạng thái thật (xanh lime = đang kết nối)
            CameraSetting.Instance.CameraOpened += (s, a) => Dispatcher.BeginInvoke(new Action(UpdateConnectionStatus));
            statusTimer.Tick += (s, a) => UpdateConnectionStatus();
            statusTimer.Start();
        }

        private readonly System.Windows.Threading.DispatcherTimer statusTimer = new System.Windows.Threading.DispatcherTimer()
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        private static readonly System.Windows.Media.Brush ConnectedBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x06, 0xC7, 0x55)); // xanh LINE
        private static readonly System.Windows.Media.Brush DisconnectedBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x80, 0xC4, 0xE9)); // cùng màu icon khác

        // Camera: mở được và còn nhận khung hình trong 3 s gần nhất. COM: cổng đã mở thành công và chưa lỗi ghi.
        public void UpdateConnectionStatus()
        {
            bool camOk = CameraSetting.Instance.IsCameraOpen
                         && (DateTime.Now - CameraSetting.Instance.LastFrameTime).TotalSeconds < 3;
            reconnectCameraIcon.Foreground = camOk ? ConnectedBrush : DisconnectedBrush;

            bool comOk = device != null && device.IsConnected;
            reconnectComIcon.Foreground = comOk ? ConnectedBrush : DisconnectedBrush;
        }

        // Hiện tên model đang mở / vừa lưu ở thanh tiêu đề (bên trái 2 nút Open / Save)
        public void SetModelName(string name)
        {
            modelNameText.Text = string.IsNullOrEmpty(name) ? "NO MODEL" : name.ToUpperInvariant();
        }

        // Nút Info cuối sidebar: popup 2 logo + phiên bản
        private void InfoBtn_Click(object sender, RoutedEventArgs e)
        {
            var win = new AboutWindow { Owner = this };
            win.ShowDialog();
        }

        // Thanh bottom: đóng và mở lại camera (rớt USB, đổi cổng...). Thông số camera hiện tại được áp lại sau khi mở.
        private async void ReconnectCamera_Click(object sender, RoutedEventArgs e)
        {
            reconnectCameraBtn.IsEnabled = false;
            try
            {
                await CameraSetting.Instance.RestartCamera();
            }
            catch (Exception)
            {
            }
            finally
            {
                reconnectCameraBtn.IsEnabled = true;
                UpdateConnectionStatus();
            }
        }

        // Thanh bottom: đóng và mở lại cổng COM đã chọn ở trang Setting (đèn trạng thái ở cả 2 nơi cùng đổi màu)
        private void ReconnectCom_Click(object sender, RoutedEventArgs e)
        {
            string port = settingModel?.SettingVal?.ComPort;
            if (!string.IsNullOrEmpty(port))
            {
                device.comPort = port;
                device.CheckCommunication(settingPage.systemBoardStatus);
            }
            UpdateConnectionStatus();
        }
    }
}