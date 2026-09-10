using DirectShowLib;
using LEDVision.Camera;
using LEDVision.Model;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
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
using Brushes = System.Windows.Media.Brushes;

namespace LEDVision
{
    /// <summary>
    /// Interaction logic for AutoPage.xaml
    /// </summary>
    public partial class AutoPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public SettingPage SettingPage;

        private Model.SettingModel settingModel;

        public Model.SettingModel SettingModel
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

                    visionTester.SettingModel = settingModel;
                    LoadCounters();
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

                    visionTester.ProgramModel = programModel;
                }
            }
        }

        private VisionTest visionTest;

        public VisionTest VisionTest
        {
            get
            {
                return visionTest;
            }

            set
            {
                if (visionTest != value)
                {
        
                        visionTest = value;
                        visionTest.TestStartedEvent += VisionTest_TestStartedEvent;
                        visionTest.CapturingAndCheckingEvent += VisionTest_CapturingAndCheckingEvent;
                        visionTest.TestFinishedEvent += VisionTest_TestFinished;

                }
            }
        }

        private int totalCnt;

        public int TotalCnt
        {
            get
            {
                return PassCnt + FailCnt;
            }
        }

        private int passCnt;

        public int PassCnt
        {
            get
            {
                return passCnt;
            }
            set
            {
                if (passCnt != value)
                {
                    passCnt = value;
                    NotifyPropertyChanged(nameof(PassCnt));
                    NotifyPropertyChanged(nameof(TotalCnt));
                    SaveCounters();
                }
            }
        }

        private int failCnt;

        public int FailCnt
        {
            get
            {
                return failCnt;
            }
            set
            {
                if (failCnt != value)
                {
                    failCnt = value;
                    NotifyPropertyChanged(nameof(FailCnt));
                    NotifyPropertyChanged(nameof(TotalCnt));
                    SaveCounters();
                }
            }
        }

        // ---- Lưu / nạp bộ đếm vào setting.json ----
        private bool loadingCounters = false;

        private void SaveCounters()
        {
            if (loadingCounters || settingModel == null || settingModel.SettingVal == null) return;
            settingModel.SettingVal.PassCount = passCnt;
            settingModel.SettingVal.FailCount = failCnt;
            try { SettingPage?.SaveSettingModel(); } catch (Exception) { }
        }

        private void LoadCounters()
        {
            if (settingModel == null || settingModel.SettingVal == null) return;
            loadingCounters = true;
            try
            {
                PassCnt = settingModel.SettingVal.PassCount;
                FailCnt = settingModel.SettingVal.FailCount;
            }
            finally
            {
                loadingCounters = false;
            }
        }

        // Nút CLEAR: xóa thống kê TOTAL / PASS / FAIL (lưu luôn xuống disk)
        private void ClearCounters_Click(object sender, RoutedEventArgs e)
        {
            PassCnt = 0;
            FailCnt = 0;
        }

        private string modelName;
        public string ModelName
        {
            get
            {
                return modelName;
            }
            set
            {
                if (modelName != value)
                {
                    modelName = value;
                    NotifyPropertyChanged(nameof(ModelName));
                }
            }
        }

        private Timer obtainFrameTimer = new Timer()
        {
            Interval = 100
        };

        public AutoPage()
        {
            InitializeComponent();
            DataContext = this;
            obtainFrameTimer.Elapsed += ObtainFrameTimer_Elapsed;
            obtainFrameTimer.Start();
        }

        private void VisionTest_CapturingAndCheckingEvent(object sender, EventArgs e)
        {
            if (CameraSetting.Instance.LastMatFrame != null)
            {
                Dispatcher.Invoke(new Action(() =>
                {                

                    var results = visionTester.ProgramModel.Vision.InspectAll();


                    if (!(results.Item1.All(item => item) && results.Item2.All(item => item) && results.Item3.All(item => item)))
                    {
                        VisionTest.FailCount += 1;
                        Console.WriteLine("fail");

                    }

                }));
            }

        }

        private void VisionTest_TestStartedEvent(object sender, EventArgs e)
        {

            VisionTest.Device.CylinderDown = false;
            VisionTest.Device.CylinderUp = false;
            VisionTest.Device.Power = false;
            VisionTest.Device.SendControl();

            // Showing Testing banner
            Dispatcher.Invoke(new Action(() =>
            {
                readyPopup.Visibility = Visibility.Collapsed;
                passPopup.Visibility = Visibility.Collapsed;
                ngPopup.Visibility = Visibility.Collapsed;
                testingPopup.Visibility = Visibility.Visible;

                foreach (var led in visionTester.ProgramModel.Vision.FourLED.Colection)
                {
                    led.Roi.Stroke = Brushes.White;

                    //cntsize too
                }
                foreach (var led in visionTester.ProgramModel.Vision.SevenSEG.Colection)
                {
                    led.Roi.Stroke = Brushes.White;
                    //cntsize too
                }
                foreach (var led in visionTester.ProgramModel.Vision.DecimalPoint.Colection)
                {
                    led.Roi.Stroke = Brushes.White;
                    //cntsize too
                }

              

            }));

            // Wait for down
            Task.Delay(1000).Wait();

            VisionTest.Device.TriggerTest = false;
            VisionTest.Device.Power = true;
            VisionTest.Device.SendControl();

            Task.Delay((int)settingModel.SettingVal.WaitRetest).Wait();

         


            // Switch to Testing Stage
            VisionTest.FailCount = 0;
            VisionTest.CurrentTestState = TestState.Testing;
            if (VisionTest.Device != null) VisionTest.Device.TreatTimeoutAsDisconnect = true;
        }

        private void VisionTest_TestFinished(object sender, EventArgs e)
        {
            string path = SettingModel.SettingVal.LogDirectory + "\\" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".jpg"; ;
            string datetime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            if (VisionTest.PostTestNG)
            {
                FailCnt += 1;



                Task.Delay(1000).Wait();

                Dispatcher.Invoke(new Action(() =>
                {
                    // Only capture when NG
                    CaptureCanvasArea(path);

                    // Showing NG banner 
                    testingPopup.Visibility = Visibility.Collapsed;
                    passPopup.Visibility = Visibility.Collapsed;
                    ngPopup.Visibility = Visibility.Visible;

                }));


            }
            else
            {
                PassCnt += 1;
                Dispatcher.Invoke(new Action(() =>
                {
                    // Showing PASS banner 
                    testingPopup.Visibility = Visibility.Collapsed;
                    ngPopup.Visibility = Visibility.Collapsed;

                    passPopup.Visibility = Visibility.Visible;

                }));

                // if test result is PASS then reseting cylinder
                VisionTest.Device.Power = false;
                VisionTest.Device.CylinderDown = false;
                VisionTest.Device.CylinderUp = true;
                VisionTest.Device.SendControl();

                Task.Delay(1000).Wait();

     


            }

            VisionTest.PostTestNG = false;
  

            // Switch to Ready Stage
            VisionTest.Device.TriggerTest = false;
            VisionTest.CurrentTestState = TestState.Ready;
            if (VisionTest.Device != null) VisionTest.Device.TreatTimeoutAsDisconnect = false;

            Task.Delay(100).Wait();

            VisionTest.Device.CylinderDown = false;
            VisionTest.Device.CylinderUp = false;
            VisionTest.Device.SendControl();

            SettingPage.IncreaseTimes();
        
        }

        private void CaptureCanvasArea(string filePath)
        {
            System.Windows.Point windowPosition = new System.Windows.Point(0, 0);
            // Get the absolute position of the Canvas on the screen
            Dispatcher.Invoke(new Action(() =>
            {
                windowPosition = mainCanvas.PointToScreen(new System.Windows.Point(0, 0));
            }));
            int x = (int)windowPosition.X;
            int y = (int)windowPosition.Y;
            int width = (int)mainCanvas.ActualWidth;
            int height = (int)(mainCanvas.ActualHeight * 0.805);

            // Create a bitmap to hold the screenshot
            using (Bitmap bitmap = new Bitmap(width, height))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    // Capture the specified area of the screen
                    graphics.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height));

                }

                try
                {
                    bitmap.Save(filePath, ImageFormat.Jpeg);

                }
                catch
                {
                    MessageBox.Show("Log file path does not exist");

                }
                // Save the bitmap as a JPG file
            }
        }

        public void DisableObtainFrameTimer()
        {
            obtainFrameTimer.Stop();
        }

        public void EnableObtainFrameTimer()
        {
            obtainFrameTimer.Start();
        }

        private bool surfaceSized = false;
        private const double DisplayMaxW = 680;
        private const double DisplayMaxH = 906;

        // Khung hiển thị/test giữ ĐÚNG tỉ lệ ảnh thô (cùng công thức với VisionPage) để toạ độ ROI
        // khớp giữa trang tạo và trang test; crop quy về ảnh thô bằng scale đồng nhất.
        private void EnsureSurfaceSized()
        {
            if (surfaceSized) return;
            var frame = CameraSetting.Instance.LastMatFrame;
            if (frame == null || frame.Width <= 0 || frame.Height <= 0) return;

            double fw = frame.Width;
            double fh = frame.Height;
            double scale = Math.Min(DisplayMaxW / fw, DisplayMaxH / fh);
            double dw = fw * scale;
            double dh = fh * scale;

            mainCanvas.Width = dw;
            mainCanvas.Height = dh;
            cameraViewer.Width = dw;
            cameraViewer.Height = dh;
            visionTester.SetSurfaceSize(dw, dh);

            surfaceSized = true;
        }

        private int frameTickBusy = 0;

        // Bỏ qua tick nếu tick trước chưa chạy xong: tránh dồn nhiều Dispatcher.Invoke làm UI bị đơ (nhất là lúc chuyển trang)
        private void ObtainFrameTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (System.Threading.Interlocked.CompareExchange(ref frameTickBusy, 1, 0) != 0) return;
            try
            {
                ObtainFrameTick();
            }
            catch (Exception)
            {
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref frameTickBusy, 0);
            }
        }

        private void ObtainFrameTick()
        {
            Dispatcher.Invoke(new Action(() =>
            {
                EnsureSurfaceSized();

                if (CameraSetting.Instance.LastFrame != null)
                {
                    try
                    {
                        cameraViewer.Source = CameraSetting.Instance.LastFrame;

                    }
                    catch
                    {

                    }

                }

            }));
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            VisionTest.startManual = true;
        }

        // Buộc chạy test thủ công khi không có tín hiệu trigger gửi tới
        private void TestForce_Click(object sender, RoutedEventArgs e)
        {
            VisionTest.startManual = true;
        }

        private void ResetCurNum_Click(object sender, RoutedEventArgs e)
        {
            SettingPage.ResetTimes();

        }
    }
}