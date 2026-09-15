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
                    // Model vừa nạp: không hiện ROI nào (chỉ hiện trong lúc step VISION CHECK chạy)
                    try
                    {
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
            bool pass = SequenceRunner.Run(steps, VisionTest.Device, model.Vision, Dispatcher, CaptureVisionStep);
            if (!pass && !VisionTest.CancelRequested)
            {
                VisionTest.FailCount += 1;
                Console.WriteLine("fail");
            }
        }

        // Takt tổng: mốc bắt đầu lượt test (retest không đặt lại), đóng băng khi có kết quả / hủy
        private DateTime taktStart = DateTime.MinValue;
        private bool taktRunning = false;

        private void UpdateTakt()
        {
            if (taktText == null || !taktRunning) return;
            taktText.Text = (DateTime.Now - taktStart).TotalSeconds.ToString("0.00") + " s";
        }

        private void StopTakt()
        {
            if (!taktRunning) return;
            taktRunning = false;
            Dispatcher.Invoke(new Action(() => taktText.Text = (DateTime.Now - taktStart).TotalSeconds.ToString("0.00") + " s"));
        }

        private void VisionTest_TestStartedEvent(object sender, EventArgs e)
        {
            if (!testBusy)
            {
                taktStart = DateTime.Now;   // lượt mới (TestStarted cũng được gọi lại ở mỗi retest → không reset)
                taktRunning = true;
                lock (logShots) logShots.Clear();
            }
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
                CloseResultWindow();   // đang test thì cửa sổ kết quả cũ tự tắt
                readyPopup.Visibility = Visibility.Collapsed;
                passPopup.Visibility = Visibility.Collapsed;
                ngPopup.Visibility = Visibility.Collapsed;
                testingPopup.Visibility = Visibility.Visible;

                // Bắt đầu test: ẩn hết ROI; tới step VISION CHECK nào thì SequenceRunner chỉ hiện ROI của group đó
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
            StopTakt();
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
            StopTakt();
            testBusy = false;
            string path = SettingModel.SettingVal.LogDirectory + "\\" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".jpg"; ;
            string datetime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Kết thúc: hiện lại TẤT CẢ ROI với màu kết quả (xanh OK / đỏ NG / xám chưa kiểm tra); ảnh ghép xem bằng nút RESULT
            Dispatcher.Invoke(new Action(() =>
            {
                foreach (var led in visionTester.ProgramModel.Vision.AllLeds())
                {
                    if (led.Roi == null) continue;
                    switch (led.ResultFinal)
                    {
                        case SingleLED.RESULT.OK: led.Roi.Stroke = SingleLED.PassBrush; break;
                        case SingleLED.RESULT.NG: led.Roi.Stroke = Brushes.Red; break;
                        default: led.Roi.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9A, 0xA5, 0xB1)); break;
                    }
                    led.Roi.Visibility = Visibility.Visible;
                }
            }));

            bool ng = VisionTest.PostTestNG;
            bool wantLog = ng ? (SettingModel?.SettingVal?.LogImageNg ?? true) : (SettingModel?.SettingVal?.LogImagePass ?? false);

            if (ng)
            {
                FailCnt += 1;
                Task.Delay(1000).Wait();
                Dispatcher.Invoke(new Action(() =>
                {
                    UpdateResultImage();
                    // Ảnh log NG (nếu bật ở Setting): ghép các ảnh đã chụp ở từng step VISION CHECK; không có ảnh nào thì chụp màn hình như cũ
                    if (wantLog && !SaveLogImage(path)) CaptureCanvasArea(path);

                    // Showing NG banner
                    testingPopup.Visibility = Visibility.Collapsed;
                    passPopup.Visibility = Visibility.Collapsed;
                    ngPopup.Visibility = Visibility.Visible;
                }));

                // Hộp thoại khóa: người vận hành phải bấm CONFIRM mới về READY
                Dispatcher.Invoke(new Action(ShowNgDialog));
            }
            else
            {
                PassCnt += 1;
                Dispatcher.Invoke(new Action(() =>
                {
                    UpdateResultImage();
                    // Ảnh log PASS (nếu bật ở Setting)
                    if (wantLog) SaveLogImage(path);

                    // Showing PASS banner
                    testingPopup.Visibility = Visibility.Collapsed;
                    ngPopup.Visibility = Visibility.Collapsed;
                    passPopup.Visibility = Visibility.Visible;
                }));
            }

            // Kết thúc (PASS hay NG): tắt nguồn LED và cả 5 relay. PASS thì nhấc jig lên; NG giữ jig ở dưới.
            VisionTest.Device.Power = false;
            VisionTest.Device.CylinderDown = false;
            VisionTest.Device.CylinderUp = !ng;
            VisionTest.Device.SendControl();
            try { VisionTest.Device.AllRelaysOff(); } catch (Exception) { }
            if (!ng) Task.Delay(1000).Wait();
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

        // ====== Ảnh log: chụp khung camera (ảnh + ROI xanh / đỏ) sau mỗi step VISION CHECK, cuối lượt ghép thành một tấm ======
        private readonly List<KeyValuePair<string, BitmapSource>> logShots = new List<KeyValuePair<string, BitmapSource>>();

        // Gọi từ luồng nền của SequenceRunner ngay sau khi step VISION CHECK có kết quả
        private void CaptureVisionStep(TestStep step)
        {
            string label = step.No + ". " + step.Label + "   " + step.Value + "   " + step.Result;
            int retest = VisionTest != null ? VisionTest.retest : 0;
            if (retest > 0) label += "   (retest " + retest + ")";
            Dispatcher.Invoke(new Action(() =>
            {
                var img = RenderCameraCanvas();
                if (img != null) lock (logShots) logShots.Add(new KeyValuePair<string, BitmapSource>(label, img));
            }));
        }

        // Vẽ mainCanvas (ảnh camera + ROI) ra bitmap, không phụ thuộc màn hình / cửa sổ có bị che
        private BitmapSource RenderCameraCanvas()
        {
            int w = (int)mainCanvas.ActualWidth, h = (int)mainCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return null;
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Black, null, new System.Windows.Rect(0, 0, w, h));
                dc.DrawRectangle(new VisualBrush(mainCanvas), null, new System.Windows.Rect(0, 0, w, h));
            }
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        // Ghép các ảnh đã chụp thành một tấm (xếp ngang, tối đa 4 ảnh một hàng, mỗi ảnh có dải nhãn ở trên) rồi lưu JPG.
        // Trả về false nếu chưa có ảnh nào (chuỗi step không có VISION CHECK, hoặc lỗi trước đó).
        private BitmapSource lastResultImage = null;

        // Ghép ảnh của lượt vừa xong và giữ lại cho nút RESULT (gọi trên UI thread)
        private void UpdateResultImage()
        {
            lastResultImage = ComposeLogImage();
        }

        private bool SaveLogImage(string filePath)
        {
            var img = lastResultImage ?? ComposeLogImage();
            if (img == null) return false;
            try
            {
                var enc = new JpegBitmapEncoder { QualityLevel = 88 };
                enc.Frames.Add(BitmapFrame.Create(img));
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath));
                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write)) enc.Save(fs);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("SaveLogImage: " + ex.Message);
                return false;
            }
        }

        private BitmapSource ComposeLogImage()
        {
            List<KeyValuePair<string, BitmapSource>> shots;
            lock (logShots) shots = new List<KeyValuePair<string, BitmapSource>>(logShots);
            if (shots.Count == 0) return null;
            try
            {
                const int perRow = 4, labelH = 40, gap = 6;
                int tileW = shots[0].Value.PixelWidth, tileH = shots[0].Value.PixelHeight;
                int cols = Math.Min(perRow, shots.Count);
                int rows = (shots.Count + perRow - 1) / perRow;
                int totalW = cols * tileW + (cols - 1) * gap;
                int totalH = rows * (labelH + tileH) + (rows - 1) * gap;

                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x32, 0x3F, 0x4E)), null, new System.Windows.Rect(0, 0, totalW, totalH));
                    var tf = new Typeface("Segoe UI");
                    for (int i = 0; i < shots.Count; i++)
                    {
                        int x = (i % perRow) * (tileW + gap);
                        int y = (i / perRow) * (labelH + tileH + gap);
                        bool fail = shots[i].Key.EndsWith(SequenceRunner.FAIL) || shots[i].Key.Contains("   " + SequenceRunner.FAIL);
                        var bar = new SolidColorBrush(fail ? System.Windows.Media.Color.FromRgb(0xE5, 0x48, 0x4D) : System.Windows.Media.Color.FromRgb(0x06, 0xC7, 0x55));
                        dc.DrawRectangle(bar, null, new System.Windows.Rect(x, y, tileW, labelH));
                        var ft = new FormattedText(shots[i].Key, System.Globalization.CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight, tf, 20, Brushes.White, 96);
                        ft.MaxTextWidth = tileW - 16;
                        ft.MaxLineCount = 1;
                        dc.DrawText(ft, new System.Windows.Point(x + 8, y + (labelH - ft.Height) / 2));
                        dc.DrawImage(shots[i].Value, new System.Windows.Rect(x, y + labelH, tileW, tileH));
                    }

                }
                var rtb = new RenderTargetBitmap(totalW, totalH, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(dv);
                rtb.Freeze();
                return rtb;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ComposeLogImage: " + ex.Message);
                return null;
            }
        }

        // Nút RESULT: popup ảnh ghép của lượt test gần nhất, vừa màn hình, Esc / click đóng
        private void ShowResult_Click(object sender, RoutedEventArgs e)
        {
            if (lastResultImage == null) return;
            System.Windows.Controls.Image img;
            var root = BuildResultContent(out img);
            var wa = SystemParameters.WorkArea;
            var ink = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x32, 0x3F, 0x4E));

            var win = new System.Windows.Window
            {
                Title = "Last test result",
                Content = root,
                Background = ink,
                Width = Math.Min(wa.Width * 0.9, Math.Max(900, lastResultImage.PixelWidth + 40)),
                Height = wa.Height * 0.9,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
            };
            try { win.Owner = System.Windows.Window.GetWindow(this); } catch (Exception) { }
            win.KeyDown += (s2, a) => { if (a.Key == Key.Escape) win.Close(); };
            img.MouseLeftButtonDown += (s2, a) => win.Close();
            win.Closed += (s2, a) => { if (ReferenceEquals(resultWin, win)) resultWin = null; };
            resultWin = win;
            win.Show();   // không modal: test mới bắt đầu là tự đóng
        }

        // Nội dung cửa sổ kết quả: ảnh ghép ở trên, bảng step ở dưới (dùng cho nút RESULT và hộp thoại NG)
        private DockPanel BuildResultContent(out System.Windows.Controls.Image imgOut)
        {
            var ink = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x32, 0x3F, 0x4E));

            // Ảnh ghép ở trên, bảng kết quả step (No / Step / Spec / Hold / Timeout / Value / Result / Takt) ở dưới
            var img = new System.Windows.Controls.Image { Source = lastResultImage, Stretch = Stretch.Uniform, Margin = new Thickness(12, 12, 12, 6) };

            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE6, 0xEB, 0xF0)),
                RowBackground = Brushes.White,
                AlternatingRowBackground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF7, 0xF9, 0xFB)),
                BorderThickness = new Thickness(0),
                CanUserAddRows = false, CanUserResizeRows = false, CanUserSortColumns = false, CanUserReorderColumns = false,
                FontSize = 13, RowHeight = 24, Focusable = false, IsHitTestVisible = false,
                Margin = new Thickness(12, 0, 12, 12),
                MaxHeight = 320,
                ItemsSource = visionTester.ProgramModel != null ? visionTester.ProgramModel.TestSteps : null,
            };
            var hdr = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            hdr.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF4, 0xF6, 0xF8))));
            hdr.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, ink));
            hdr.Setters.Add(new Setter(System.Windows.Controls.Control.FontWeightProperty, FontWeights.SemiBold));
            hdr.Setters.Add(new Setter(System.Windows.Controls.Control.PaddingProperty, new Thickness(6, 4, 6, 4)));
            grid.ColumnHeaderStyle = hdr;
            Func<string, string, double, DataGridTextColumn> col = (h, path, w) =>
                new DataGridTextColumn { Header = h, Binding = new System.Windows.Data.Binding(path), Width = w > 0 ? new DataGridLength(w) : new DataGridLength(1, DataGridLengthUnitType.Star) };
            grid.Columns.Add(col("No", "No", 40));
            grid.Columns.Add(col("Step", "Label", 0));
            grid.Columns.Add(col("Spec", "Spec", 70));
            grid.Columns.Add(col("Hold", "Hold", 60));
            grid.Columns.Add(col("Timeout", "Timeout", 70));
            grid.Columns.Add(col("Value", "Value", 110));
            var resCol = col("Result", "Result", 70);
            var resStyle = new Style(typeof(TextBlock));
            resStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
            resStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
            resStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6B, 0x7A, 0x8C))));
            var tPass = new DataTrigger { Binding = new System.Windows.Data.Binding("Result"), Value = SequenceRunner.PASS };
            tPass.Setters.Add(new Setter(TextBlock.ForegroundProperty, SingleLED.PassBrush));
            var tFail = new DataTrigger { Binding = new System.Windows.Data.Binding("Result"), Value = SequenceRunner.FAIL };
            tFail.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5, 0x48, 0x4D))));
            resStyle.Triggers.Add(tPass);
            resStyle.Triggers.Add(tFail);
            resCol.ElementStyle = resStyle;
            grid.Columns.Add(resCol);
            grid.Columns.Add(col("Takt (ms)", "Takt", 80));

            var root = new DockPanel();
            DockPanel.SetDock(grid, Dock.Bottom);
            root.Children.Add(grid);
            root.Children.Add(img);
            imgOut = img;
            return root;
        }

        // Hộp thoại KHÓA khi NG: modal, phải bấm CONFIRM mới về READY (trigger cảm biến / START không có tác dụng trong lúc này)
        private void ShowNgDialog()
        {
            var wa = SystemParameters.WorkArea;
            var red = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5, 0x48, 0x4D));
            var ink = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x32, 0x3F, 0x4E));

            var outer = new DockPanel();
            var header = new Border { Background = red, Padding = new Thickness(0, 10, 0, 10) };
            header.Child = new TextBlock { Text = "NG", FontSize = 40, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
            DockPanel.SetDock(header, Dock.Top);
            outer.Children.Add(header);

            var confirm = new Button
            {
                Content = "CONFIRM",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = red,
                BorderThickness = new Thickness(0),
                Height = 64,
                Margin = new Thickness(12, 6, 12, 12),
                Cursor = Cursors.Hand,
                Focusable = true,
            };
            DockPanel.SetDock(confirm, Dock.Bottom);
            outer.Children.Add(confirm);

            // Chỉ ảnh ghép, KHÔNG có bảng step
            if (lastResultImage != null)
            {
                outer.Children.Add(new System.Windows.Controls.Image { Source = lastResultImage, Stretch = Stretch.Uniform, Margin = new Thickness(12) });
            }
            else
            {
                outer.Children.Add(new TextBlock { Text = "No image", Foreground = Brushes.White, FontSize = 18, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            }

            var win = new System.Windows.Window
            {
                Title = "NG",
                Content = outer,
                Background = ink,
                Width = wa.Width * 0.9,
                Height = wa.Height * 0.9,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = System.Windows.WindowStyle.None,
                Topmost = true,
            };
            try { win.Owner = System.Windows.Window.GetWindow(this); } catch (Exception) { }
            confirm.Click += (s2, a) => win.Close();
            win.KeyDown += (s2, a) => { if (a.Key == Key.Enter) win.Close(); };
            win.Loaded += (s2, a) => confirm.Focus();
            CloseResultWindow();
            win.ShowDialog();   // chặn tới khi CONFIRM
        }

        private System.Windows.Window resultWin = null;

        private void CloseResultWindow()
        {
            try { if (resultWin != null) resultWin.Close(); } catch (Exception) { }
            resultWin = null;
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
                UpdateTakt();

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