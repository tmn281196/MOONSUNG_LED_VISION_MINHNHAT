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
                    try { stepsGrid.ItemsSource = programModel != null ? programModel.TestSteps : null; } catch (Exception) { }
                    // Model vừa nạp: chưa test thì không hiện ROI nào (chỉ hiện theo step VISION CHECK hoặc khi bật toggle mắt)
                    try
                    {
                        if (allRoiBtn != null) allRoiBtn.IsChecked = false;
                        if (programModel != null)
                            foreach (var led in programModel.Vision.AllLeds())
                                if (led.Roi != null) led.Roi.Visibility = Visibility.Collapsed;
                    }
                    catch (Exception) { }
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
                        visionTest.TestCancelledEvent += VisionTest_TestCancelled;

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

        // Chạy chuỗi step của model (POWER / RELAY / DELAY / VISION CHECK). Một step FAIL = lượt test NG.
        private void VisionTest_CapturingAndCheckingEvent(object sender, EventArgs e)
        {
            var model = visionTester.ProgramModel;
            if (model == null) return;
            var steps = model.TestSteps;
            if (steps == null || steps.Count == 0)
            {
                VisionTest.FailCount += 1;   // không có step nào → NG (model chưa có sequence)
                return;
            }
            Model.TestStepList.ClearResults(steps);
            bool pass = SequenceRunner.Run(steps, VisionTest.Device, model.Vision, Dispatcher);
            if (!pass && !VisionTest.CancelRequested)
            {
                VisionTest.FailCount += 1;
                Console.WriteLine("fail");
            }
        }

        private void VisionTest_TestStartedEvent(object sender, EventArgs e)
        {
            testBusy = true;
            VisionTest.CancelRequested = false;

            // Ngay trước test: tắt nguồn LED và mọi relay (lượt NG trước có thể còn để bật)
            VisionTest.Device.CylinderDown = false;
            VisionTest.Device.CylinderUp = false;
            VisionTest.Device.Power = false;
            VisionTest.Device.SendControl();
            try { VisionTest.Device.AllRelaysOff(); } catch (Exception) { }

            // Showing Testing banner
            Dispatcher.Invoke(new Action(() =>
            {
                readyPopup.Visibility = Visibility.Collapsed;
                passPopup.Visibility = Visibility.Collapsed;
                ngPopup.Visibility = Visibility.Collapsed;
                testingPopup.Visibility = Visibility.Visible;

                // Bắt đầu test: ẩn hết ROI; tới step VISION CHECK nào thì SequenceRunner chỉ hiện ROI của group đó
                allRoiBtn.IsChecked = false;
                foreach (var led in visionTester.ProgramModel.Vision.AllLeds())
                {
                    led.ResultFinal = SingleLED.RESULT.UNKNOWN;
                    led.Roi.Visibility = Visibility.Collapsed;
                }
                Model.TestStepList.ClearResults(visionTester.ProgramModel.TestSteps);

            }));

            // Chờ cảm biến DOWN xác nhận xi lanh đã xuống hẳn (tối đa 3 s), thêm 200 ms cho hết rung
            VisionTest.Device.WaitForDown(settingModel?.SettingVal?.SensorTimeoutMs ?? 3000);
            // Không còn khoảng chờ cố định trước chuỗi step: cần chờ thì thêm step DELAY đầu chuỗi (Sequence page)

            VisionTest.Device.TriggerTest = false;

            // Bật nguồn / chờ / kiểm tra camera do chuỗi step của model quyết định (trang Sequence)

            // Switch to Testing Stage
            VisionTest.FailCount = 0;
            VisionTest.CurrentTestState = TestState.Testing;
            if (VisionTest.Device != null) VisionTest.Device.TreatTimeoutAsDisconnect = true;
        }

        // Test bị hủy: tắt nguồn, thả xi lanh, hiện READY, KHÔNG tính PASS / FAIL, không tăng số lần dùng pin
        private void VisionTest_TestCancelled(object sender, EventArgs e)
        {
            testBusy = false;
            VisionTest.Device.Power = false;
            VisionTest.Device.CylinderDown = false;
            VisionTest.Device.CylinderUp = false;
            VisionTest.Device.SendControl();
            try { VisionTest.Device.AllRelaysOff(); } catch (Exception) { }

            Dispatcher.Invoke(new Action(() =>
            {
                testingPopup.Visibility = Visibility.Collapsed;
                passPopup.Visibility = Visibility.Collapsed;
                ngPopup.Visibility = Visibility.Collapsed;
                readyPopup.Visibility = Visibility.Visible;
            }));

            VisionTest.PostTestNG = false;
            VisionTest.Device.IgnoreTrigger = false;
            VisionTest.Device.TriggerTest = false;
            if (SettingPage != null && SettingPage.MainWindowRef != null) SettingPage.MainWindowRef.LastReadyTime = DateTime.Now;
            VisionTest.CurrentTestState = TestState.Ready;
            if (VisionTest.Device != null) VisionTest.Device.TreatTimeoutAsDisconnect = false;
        }

        private void VisionTest_TestFinished(object sender, EventArgs e)
        {
            testBusy = false;
            string path = SettingModel.SettingVal.LogDirectory + "\\" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".jpg"; ;
            string datetime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Kết thúc: giữ nguyên màn hình ROI của step VISION CHECK cuối cùng đã chạy (không hiện lại các group khác)

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

            // PASS: nguồn LED đã tắt ở trên, tắt nốt 5 relay. NG: GIỮ NGUYÊN nguồn / relay / xi lanh như lúc chuỗi step dừng
            // (để người vận hành nhìn được trạng thái lỗi); chỉ EMERGENCY STOP hoặc lượt test kế tiếp mới tắt.
            if (!VisionTest.PostTestNG)
            {
                try { VisionTest.Device.AllRelaysOff(); } catch (Exception) { }
            }
            VisionTest.PostTestNG = false;

            // Switch to Ready Stage: xóa trigger còn sót + mở cửa sổ chặn 1,5 s (cạnh reed tới trễ sau khi xi lanh về)
            VisionTest.Device.IgnoreTrigger = false;
            VisionTest.Device.TriggerTest = false;
            if (SettingPage != null && SettingPage.MainWindowRef != null) SettingPage.MainWindowRef.LastReadyTime = DateTime.Now;
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

        private string lastCylinderState = null;

        private void UpdateCylinderIndicator()
        {
            var dev = VisionTest?.Device;
            if (dev == null || cylinderText == null) return;
            string state;
            System.Windows.Media.Brush brush;
            if (!dev.IsConnected)
            {
                state = "--";
                brush = System.Windows.Media.Brushes.Gray;
            }
            else if (dev.SS_DOWN)
            {
                state = "DOWN";
                brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x06, 0xC7, 0x55));
            }
            else
            {
                // Chỉ có một cảm biến (dưới): không phát hiện = xi lanh đang ở trên
                state = "UP";
                brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x7A, 0xCC));
            }
            if (state == lastCylinderState) return;
            lastCylinderState = state;
            cylinderText.Text = state;
            cylinderDot.Fill = brush;
        }

        private void ObtainFrameTick()
        {
            Dispatcher.Invoke(new Action(() =>
            {
                EnsureSurfaceSized();
                UpdateCylinderIndicator();
                UpdateStartButton();

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
        // true từ lúc test bắt đầu (chờ xi lanh, bật nguồn) tới khi kết thúc / hủy → nút hiện CANCEL
        private volatile bool testBusy = false;

        // Nút "?": hướng dẫn trang Auto (EN / TH) kèm timeline chuỗi test
        // Toggle mắt: bật = hiện mọi ROI với kết quả gần nhất (xanh OK / đỏ NG / xám chưa kiểm tra); tắt = ẩn hết
        private static readonly System.Windows.Media.Brush UncheckedRoiBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9A, 0xA5, 0xB1));

        private void AllRoi_Click(object sender, RoutedEventArgs e)
        {
            bool on = allRoiBtn.IsChecked == true;
            var model = visionTester.ProgramModel;
            if (model == null) return;
            foreach (var led in model.Vision.AllLeds())
            {
                if (led.Roi == null) continue;
                if (!on)
                {
                    led.Roi.Visibility = Visibility.Collapsed;
                    continue;
                }
                led.Roi.Visibility = Visibility.Visible;
                switch (led.ResultFinal)
                {
                    case SingleLED.RESULT.OK: led.Roi.Stroke = SingleLED.PassBrush; break;
                    case SingleLED.RESULT.NG: led.Roi.Stroke = Brushes.Red; break;
                    default: led.Roi.Stroke = UncheckedRoiBrush; break;
                }
            }
        }

        // START: chạy test bằng tay. Đang test: CANCEL → hủy theo cùng cơ chế với cảm biến rời đáy
        private void TestForce_Click(object sender, RoutedEventArgs e)
        {
            if (testBusy || VisionTest.CurrentTestState == TestState.Testing)
            {
                VisionTest.CancelRequested = true;
            }
            else
            {
                VisionTest.startManual = true;
            }
        }

        private void UpdateStartButton()
        {
            if (startBtn == null) return;
            bool busy = testBusy || VisionTest?.CurrentTestState == TestState.Testing;
            string text = busy ? "EMERGENCY STOP" : "START";
            if (!Equals(startBtn.Content, text))
            {
                startBtn.Content = text;
                if (busy)
                {
                    startBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5, 0x48, 0x4D));
                    startBtn.Foreground = System.Windows.Media.Brushes.White;
                }
                else
                {
                    startBtn.ClearValue(BackgroundProperty);
                    startBtn.ClearValue(ForegroundProperty);
                }
            }
        }

        private void ResetCurNum_Click(object sender, RoutedEventArgs e)
        {
            SettingPage.ResetTimes();

        }
    }
}