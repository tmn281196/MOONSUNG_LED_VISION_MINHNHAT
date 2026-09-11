using LEDVision.Camera;
using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Wpf;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;
using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;
using TextBox = System.Windows.Controls.TextBox;

namespace LEDVision
{
    /// <summary>
    /// Interaction logic for this.xaml
    /// </summary>
    ///
    public class HueConverter : IValueConverter
    {
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {

            if (double.TryParse(value as string, out double val179))
            {
                return (val179 * 360.0 / 179);
            }
            return 0;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double val360)
            {
                return (val360 * 179 / 360.0).ToString("F0"); // format with 1 decimal
            }
            return "0";
        }
    }

    public class SaturationConverter : IValueConverter
    {
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {

            if (double.TryParse(value as string, out double val255))
            {
                return (val255 / 255);
            }
            return 0;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double val100)
            {
                return (val100 * 255.0).ToString("F0"); // format with 1 decimal
            }
            return "0";
        }
    }
    public class ValuePercentTo255Converter : IValueConverter
    {
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {

            if (double.TryParse(value as string, out double val255))
            {
                return (val255 / 255);
            }
            return 0;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double val100)
            {
                return (val100 * 255.0).ToString("F0"); // format with 1 decimal
            }
            return "0";
        }
    }
    public partial class VisionPage : Page
    {
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
                    InitPersistUI();
                }
            }
        }

        // ===== Persist (chống nhấp nháy kết quả) =====
        private bool loadingPersist = false;

        // Nạp trạng thái persist từ setting.json lên UI + đồng bộ cấu hình tĩnh.
        private void InitPersistUI()
        {
            if (settingModel == null || settingModel.SettingVal == null) return;
            if (persistCheckBox == null || persistFramesBox == null) return;

            loadingPersist = true;
            persistCheckBox.IsChecked = settingModel.SettingVal.PersistEnabled;
            persistFramesBox.Text = settingModel.SettingVal.PersistMs.ToString();
            loadingPersist = false;

            Camera.SingleLED.PersistEnabled = settingModel.SettingVal.PersistEnabled;
            Camera.SingleLED.PersistMs = settingModel.SettingVal.PersistMs;
            ResetPersistCounters();
        }

        // ---- Persist: bộ đếm + giai đoạn ổn định (học theo SurfaceInspection) ----
        private const int PreviewIntervalMs = 250;   // nhịp timer xem trực tiếp
        private int framesSinceReset = 0;

        // Đổi tham số (HSV, ngưỡng, bán kính, nhóm...) → số đếm cũ vô nghĩa → xóa và đếm lại từ đầu
        private void ResetPersistCounters()
        {
            try { ProgramModel?.Vision?.ResetPersistAll(); } catch (Exception) { }
            framesSinceReset = 0;
        }

        // Gọi mỗi tick xem trực tiếp: đặt số khung theo nhịp 250 ms, cập nhật chữ "settling"
        private void TickPersist()
        {
            Camera.SingleLED.PersistFrames = Camera.SingleLED.FramesFor(PreviewIntervalMs);
            bool active = Camera.SingleLED.PersistEnabled && Camera.SingleLED.PersistFrames > 1;
            if (active && framesSinceReset < Camera.SingleLED.PersistFrames) framesSinceReset++;
            bool settling = active && framesSinceReset < Camera.SingleLED.PersistFrames;
            if (persistSettlingText != null)
            {
                var v = settling ? Visibility.Visible : Visibility.Collapsed;
                if (persistSettlingText.Visibility != v) persistSettlingText.Visibility = v;
            }
        }

        private void Persist_Changed(object sender, RoutedEventArgs e)
        {
            ApplyPersistSettings();
        }

        private void PersistFrames_Changed(object sender, RoutedEventArgs e)
        {
            ApplyPersistSettings();
        }

        // Lưu cấu hình persist vào setting.json + áp dụng cho inspection.
        private void ApplyPersistSettings()
        {
            if (loadingPersist) return;
            if (settingModel == null || settingModel.SettingVal == null) return;

            settingModel.SettingVal.PersistEnabled = persistCheckBox.IsChecked == true;

            if (int.TryParse(persistFramesBox.Text, out int ms) && ms >= 0 && ms <= 5000)
            {
                settingModel.SettingVal.PersistMs = ms;
            }
            else
            {
                persistFramesBox.Text = settingModel.SettingVal.PersistMs.ToString();
            }

            Camera.SingleLED.PersistEnabled = settingModel.SettingVal.PersistEnabled;
            Camera.SingleLED.PersistMs = settingModel.SettingVal.PersistMs;
            ResetPersistCounters();

            try { mainWindow?.settingPage?.SaveSettingModel(); } catch { }
        }

        private Model.Model programModel;

        public Model.Model ProgramModel
        {
            get
            {
                return builder.ProgramModel;
            }
            set
            {
                if (programModel != value)
                {
                    programModel = value;
                    builder.ProgramModel = programModel.Clone();
                    BindCameraSettings();
                    RefreshGroupList();
                    SelectGroup(null);
                    RefreshGroupList();
                    SelectGroup(null);
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
                }
            }
        }

        public bool ng = false;

        private System.Timers.Timer obtainFrameTimer = new System.Timers.Timer()
        {
            Interval = 250
        };

        private ScaleTransform scaleTransform;
        private TranslateTransform translateTransform;
        private const double MinScale = 1.0;
        private const double MaxScale = 5.0;

        private MainWindow mainWindow;

        public VisionPage(MainWindow mainWindow)
        {
            InitializeComponent();
            HistogramAnalyzing();
            BindCameraSettings();
            CameraSetting.Instance.CameraOpened += (s, e) => Dispatcher.BeginInvoke(new Action(ApplyCameraRanges));
            this.PreviewKeyDown += VisionPage_PreviewKeyDown;

            this.mainWindow = mainWindow;
            obtainFrameTimer.Elapsed += ObtainFrameTimer_Elapsed;
            TransformGroup transformGroup = (TransformGroup)this.FindResource("sharedTransform");
            scaleTransform = (ScaleTransform)transformGroup.Children[0];
            translateTransform = (TranslateTransform)transformGroup.Children[1];


        }

        public void DisableObtainFrameTimer()
        {
            obtainFrameTimer.Stop();
        }

        public void EnableObtainFrameTimer()
        {
            obtainFrameTimer.Start();
        }

        // Hàm tìm điểm đầu và cuối mà histogram có giá trị > 0
        private (float min, float max) GetHistogramRange(Mat hist, int totalBins, float rangeMax)
        {
            float min = 0;
            float max = rangeMax;

            // Tìm điểm bắt đầu (min) - tìm từ trái sang phải
            for (int i = 0; i < totalBins; i++)
            {
                if (hist.At<float>(i) > 0)
                {
                    min = (i * rangeMax) / totalBins;
                    break;
                }
            }

            // Tìm điểm kết thúc (max) - tìm từ phải sang trái
            for (int i = totalBins - 1; i >= 0; i--)
            {
                if (hist.At<float>(i) > 0)
                {
                    max = ((i + 1) * rangeMax) / totalBins; // +1 để lấy đến cuối bin
                    break;
                }
            }

            return (min, max);
        }


        private bool surfaceSized = false;
        private const double DisplayMaxW = 680;
        private const double DisplayMaxH = 906;

        // Khung hiển thị/vẽ ROI giữ ĐÚNG tỉ lệ ảnh thô (lấy từ frame đầu), vừa trong vùng DisplayMax.
        // ROI vẽ ở độ phân giải khung này nên stroke đẹp; lúc crop, scale frame/khung là ĐỒNG NHẤT
        // (scaleX==scaleY) nên ROI tròn quy về ảnh thô vẫn tròn, không méo.
        private void EnsureSurfaceSized(Mat frame)
        {
            if (surfaceSized || frame == null || frame.Width <= 0 || frame.Height <= 0) return;

            double fw = frame.Width;
            double fh = frame.Height;

            double scale = Math.Min(DisplayMaxW / fw, DisplayMaxH / fh);
            double dw = fw * scale;
            double dh = fh * scale;

            mainCanvas.Width = dw;
            mainCanvas.Height = dh;
            cameraViewer.Width = dw;
            cameraViewer.Height = dh;
            builder.SetSurfaceSize(dw, dh);

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
            if (CameraSetting.Instance.LastMatFrame != null)
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    EnsureSurfaceSized(CameraSetting.Instance.LastMatFrame);

                    adjustedImageViewer.Source = ProgramModel.Vision.ObtainHSVAdjustedFrame(CameraSetting.Instance.LastMatFrame.Clone(), mainCanvas);

                    try
                    {
                        TickPersist();
                        ProgramModel.Vision.Inspect(CameraSetting.Instance.LastMatFrame.Clone());
                        UpdateHistogram();

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                    cameraViewer.Source = CameraSetting.Instance.LastFrame;



                    if (builder.selectedLed != null)
                    {
                        builder.selectedLed.Roi.StrokeDashArray = new DoubleCollection() { 1, 1 };
                    }


                }));
            }

        }

        // Phím tắt ROI (Ctrl+C/V/D, Delete, mũi tên) chạy ở mọi chỗ trên trang, trừ khi đang gõ / chỉnh một ô nhập
        private void VisionPage_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var focused = Keyboard.FocusedElement;
            if (focused is System.Windows.Controls.Primitives.TextBoxBase
                || focused is System.Windows.Controls.ComboBox
                || focused is System.Windows.Controls.Slider
                || focused is System.Windows.Controls.PasswordBox)
            {
                return;
            }
            builder?.HandleShortcut(e);
        }

        private CameraSettingValues boundCameraValues;

        // Bind danh sách thông số camera (Expander) vào bộ dùng chung; kéo slider là áp lên camera ngay.
        private void BindCameraSettings()
        {
            if (boundCameraValues != null)
                boundCameraValues.PropertyChanged -= CameraValues_PropertyChanged;

            boundCameraValues = CameraSetting.Instance.cameraSettingValues;
            cameraSettingsPanel.DataContext = boundCameraValues;
            boundCameraValues.PropertyChanged += CameraValues_PropertyChanged;
            ApplyCameraRanges();
        }

        private void CameraValues_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Giá trị đang được đọc TỪ camera vào → không đẩy ngược lại camera
            if (CameraSetting.Instance.IsSyncingFromCamera) return;
            CameraSetting.Instance.SetSingle(e.PropertyName, boundCameraValues);
        }

        // Đặt Min/Max/Step của từng slider theo dải giá trị THẬT đọc từ driver camera
        // (mỗi camera khác nhau: BRIO có White Balance tới 7500, Pan/Tilt chỉ ±10...). Không đọc được thì giữ giá trị trong XAML.
        private void ApplyCameraRanges()
        {
            var ctl = CameraSetting.Instance.Control;
            if (ctl == null || !ctl.IsOpen) return;

            SetSliderRange(sldExposure, ctl.GetRange(DirectShowLib.CameraControlProperty.Exposure));
            SetSliderRange(sldFocus, ctl.GetRange(DirectShowLib.CameraControlProperty.Focus));
            SetSliderRange(sldZoom, ctl.GetRange(DirectShowLib.CameraControlProperty.Zoom));
            // Ảnh đã xoay 90°: slider ngang = Tilt, slider dọc = Pan
            SetSliderRange(sldShiftH, ctl.GetRange(DirectShowLib.CameraControlProperty.Tilt));
            SetSliderRange(sldShiftV, ctl.GetRange(DirectShowLib.CameraControlProperty.Pan));

            SetSliderRange(sldBrightness, ctl.GetRange(DirectShowLib.VideoProcAmpProperty.Brightness));
            SetSliderRange(sldContrast, ctl.GetRange(DirectShowLib.VideoProcAmpProperty.Contrast));
            SetSliderRange(sldSaturation, ctl.GetRange(DirectShowLib.VideoProcAmpProperty.Saturation));
            SetSliderRange(sldHue, ctl.GetRange(DirectShowLib.VideoProcAmpProperty.Hue));
            SetSliderRange(sldGamma, ctl.GetRange(DirectShowLib.VideoProcAmpProperty.Gamma));
            SetSliderRange(sldSharpness, ctl.GetRange(DirectShowLib.VideoProcAmpProperty.Sharpness));
            SetSliderRange(sldBacklight, ctl.GetRange(DirectShowLib.VideoProcAmpProperty.BacklightCompensation));
            SetSliderRange(sldWhiteBalance, ctl.GetRange(DirectShowLib.VideoProcAmpProperty.WhiteBalance));
            SetSliderRange(sldGain, ctl.GetRange(DirectShowLib.VideoProcAmpProperty.Gain));
        }

        private static void SetSliderRange(Slider slider, CameraPropertyRange range)
        {
            if (slider == null) return;
            if (range == null)
            {
                // Camera không hỗ trợ thông số này (BRIO: Hue, Gamma bị mờ trong hộp thoại driver)
                slider.IsEnabled = false;
                slider.ToolTip = "Not supported by this camera";
                return;
            }
            slider.IsEnabled = true;
            slider.Minimum = range.Min;
            slider.Maximum = range.Max;
            slider.TickFrequency = range.Step > 0 ? range.Step : 1;
            slider.IsSnapToTickEnabled = true;
            string baseTip = slider.Tag as string;
            slider.ToolTip = (baseTip != null ? baseTip + " " : "") + "Range " + range.Min + " ~ " + range.Max + (range.Step > 1 ? " (step " + range.Step + ")" : "");
        }

        // Đọc lại thông số ĐANG DÙNG của camera lên slider (khi chỉnh bằng phần mềm Logitech / hộp thoại driver)
        private void SyncCamera_Click(object sender, RoutedEventArgs e)
        {
            if (!CameraSetting.Instance.ReadFromCamera())
            {
                System.Windows.MessageBox.Show("Camera is not ready.", "Camera", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Mở hộp thoại thuộc tính của driver (Logitech Properties). Đóng xong tự đọc lại giá trị.
        private void DriverDialog_Click(object sender, RoutedEventArgs e)
        {
            if (!CameraSetting.Instance.ShowDriverDialog())
            {
                System.Windows.MessageBox.Show("Cannot open the camera driver dialog.", "Camera", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Nút "?": hướng dẫn thao tác ROI (EN / TH)
        private void RoiHelp_Click(object sender, RoutedEventArgs e)
        {
            var win = new RoiHelpWindow("roi");
            try { win.Owner = System.Windows.Window.GetWindow(this); } catch (Exception) { }
            win.Show();
        }

        // Nút "?" cạnh HSV Range: lý thuyết màu HSV + cách app tách màu (EN / TH)
        private void HsvHelp_Click(object sender, RoutedEventArgs e)
        {
            var win = new RoiHelpWindow("hsv");
            try { win.Owner = System.Windows.Window.GetWindow(this); } catch (Exception) { }
            win.Show();
        }

        // Lấy lại thông số từ file model đã nạp/lưu gần nhất
        private void RevertCamera_Click(object sender, RoutedEventArgs e)
        {
            CameraSetting.Instance.cameraSettingValues.CopyFrom(CameraSetting.Instance.loadedCameraSettingValues);
        }

        // Reset thông số camera về mặc định (giá trị khởi tạo của CameraSettingValues)
        private void ResetCameraDefault_Click(object sender, RoutedEventArgs e)
        {
            CameraSetting.Instance.cameraSettingValues.CopyFrom(new CameraSettingValues());
        }
        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoomFactor = e.Delta > 0 ? 0.1 : -0.1;
            double newScale = scaleTransform.ScaleX + zoomFactor;

            if (newScale < MinScale || newScale > MaxScale)
                return;

            Point cursorPosition = e.GetPosition(mainCanvas);

            double relativeX = (cursorPosition.X - translateTransform.X) / scaleTransform.ScaleX;
            double relativeY = (cursorPosition.Y - translateTransform.Y) / scaleTransform.ScaleY;

            scaleTransform.ScaleX = newScale;
            scaleTransform.ScaleY = newScale;

            translateTransform.X = cursorPosition.X - relativeX * newScale;
            translateTransform.Y = cursorPosition.Y - relativeY * newScale;
        }

        private void ResetZoomBtn_Click(object sender, RoutedEventArgs e)
        {
            scaleTransform.ScaleX = 1.0;
            scaleTransform.ScaleY = 1.0;
            translateTransform.X = 0;
            translateTransform.Y = 0;
        }
        // Save: ghi thẳng vào file model đang mở (không hỏi). Chưa có file thì hỏi như Save As.
        public void SaveModelBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveModel(false);
        }

        // Save As: luôn hỏi chọn file
        public void SaveModelAsBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveModel(true);
        }

        // JSON của model hiện tại (đúng định dạng sẽ ghi ra file) → dùng để biết model đã bị sửa hay chưa
        public string GetModelJson()
        {
            try
            {
                var m = ProgramModel;
                if (m == null) return "";
                m.CameraSettingValues = CameraSetting.Instance.cameraSettingValues;
                return System.Text.Json.JsonSerializer.Serialize(m, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception)
            {
                return "";
            }
        }

        private void SaveModel(bool saveAs)
        {
            // 1) Lấy giá trị ĐANG DÙNG THẬT của camera (người dùng có thể đã chỉnh ở hộp thoại driver / Logitech)
            CameraSetting.Instance.ReadFromCamera();
            var camValues = CameraSetting.Instance.cameraSettingValues;

            // 2) Gắn bộ thông số camera vào model sẽ lưu (và các bản model khác đang giữ trong app)
            ProgramModel.CameraSettingValues = camValues;
            if (programModel != null) programModel.CameraSettingValues = camValues;
            if (mainWindow != null && mainWindow.programModel != null) mainWindow.programModel.CameraSettingValues = camValues;

            // 3) Chọn đường dẫn: Save → file đang mở; Save As / chưa có file → hộp thoại
            string path = mainWindow?.CurrentModelPath;
            if (saveAs || string.IsNullOrEmpty(path))
            {
                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog()
                {
                    DefaultExt = ".json",
                    Filter = "Vision Model File (*.json)|*.json",
                    Title = saveAs ? "Save model as" : "Save model",
                };
                // Mở sẵn thư mục của model đang mở; chưa mở model nào thì dùng thư mục model gần nhất trong setting.json
                try
                {
                    string seed = !string.IsNullOrEmpty(path) ? path : mainWindow?.settingModel?.SettingVal?.LastModelPath;
                    if (!string.IsNullOrEmpty(seed))
                    {
                        string dir = System.IO.Path.GetDirectoryName(seed);
                        if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir)) dlg.InitialDirectory = dir;
                        if (!string.IsNullOrEmpty(path)) dlg.FileName = System.IO.Path.GetFileName(path);
                    }
                }
                catch (Exception) { }
                if (dlg.ShowDialog() != true) return;
                path = dlg.FileName;
            }

            bool ok = false;
            try
            {
                ok = Utility.SaveModel(ProgramModel, path, System.IO.Path.GetFileName(path));
            }
            catch (Exception)
            {
                ok = false;
            }

            if (!ok)
            {
                System.Windows.MessageBox.Show("Failed to save model file!\n" + path, "Save model", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            CameraSetting.Instance.loadedCameraSettingValues = camValues.Clone();
            // AutoPage / VisionTester phải nhận BẢN CLONE: các Ellipse ROI của model này đang nằm trên canvas
            // của VisionBuilder, add lại lên canvas thứ hai sẽ ném "already the logical child of another element".
            if (mainWindow != null && mainWindow.autoPage != null)
            {
                var autoModel = ProgramModel.Clone();
                autoModel.CameraSettingValues = camValues;
                mainWindow.autoPage.ProgramModel = autoModel;
                if (mainWindow.settingPage != null) mainWindow.settingPage.ProgramModel = autoModel;
                mainWindow.programModel = autoModel;
            }

            // Cập nhật tên model vừa lưu lên thanh tiêu đề + AutoPage + ghi nhớ đường dẫn
            string modelName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (mainWindow != null)
            {
                mainWindow.SetModelName(modelName);
                if (mainWindow.autoPage != null) mainWindow.autoPage.ModelName = modelName;
                mainWindow.RememberLastModel(path);
                mainWindow.MarkModelSaved();
                mainWindow.FlashSaved();
            }
        }

        private void ColorChanged()
        {
        }

        private void ColorpickerMain_ColorChanged(object sender, MouseButtonEventArgs e)
        {
            ColorChanged();
        }

        private void ColorpickerMain_ColorChanged(object sender, MouseWheelEventArgs e)
        {
            ColorChanged();
        }
        private void AdjustHSVTolerance(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }
        private void cntSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ResetPersistCounters();
            if (ProgramModel.Vision.SelectedGroupLED != null)
            {
                ProgramModel.Vision.SelectedGroupLED.ContourArea = (int)cntSizeSlider.Value;

                foreach (var item in ProgramModel.Vision.SelectedGroupLED.Colection)
                {
                    item.ResultFinal = SingleLED.RESULT.UNKNOWN;
                }
            }
        }
        // ====== Group LED động: bảng group (chọn dòng = chọn group, sửa ô Name = đổi tên) + Add / Delete / None ======
        private bool groupGridBusy = false;

        // Nạp lại bảng group (sau khi mở model / thêm / xóa), giữ group đang chọn nếu còn
        public void RefreshGroupList()
        {
            if (groupGrid == null || ProgramModel == null) return;
            groupGridBusy = true;
            try
            {
                var cur = ProgramModel.Vision.SelectedGroupLED;
                if (!ReferenceEquals(groupGrid.ItemsSource, ProgramModel.Vision.Groups)) groupGrid.ItemsSource = ProgramModel.Vision.Groups;
                groupGrid.Items.Refresh();
                groupGrid.SelectedItem = (cur != null && ProgramModel.Vision.Groups.Contains(cur)) ? cur : null;
            }
            finally
            {
                groupGridBusy = false;
            }
            try { mainWindow?.sequencePage?.RefreshGroupNames(); } catch (Exception) { }
        }

        // Chọn group để vẽ / chỉnh (null = None: khóa ảnh)
        public void SelectGroup(GroupLED g)
        {
            if (ProgramModel == null) return;
            if (g != null && !ProgramModel.Vision.Groups.Contains(g)) g = null;
            ProgramModel.Vision.SelectedGroupLED = g;
            builder.SelectedVisionObject = g;
            if (groupGrid != null && !groupGridBusy)
            {
                groupGridBusy = true;
                try
                {
                    groupGrid.SelectedItem = g;
                    if (g != null) groupGrid.ScrollIntoView(g);
                }
                finally { groupGridBusy = false; }
            }
            UpdateSettings("");
        }

        private void GroupGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (groupGridBusy) return;
            SelectGroup(groupGrid.SelectedItem as GroupLED);
        }

        // Đổi tên ngay trong ô Name: nhớ tên cũ lúc bắt đầu sửa, sửa xong thì cập nhật step VISION CHECK đang trỏ tới tên cũ
        private string groupNameBeforeEdit = null;

        private void GroupGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            var g = e.Row.Item as GroupLED;
            groupNameBeforeEdit = g != null ? g.Name : null;
        }

        private void GroupGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            var g = e.Row.Item as GroupLED;
            string old = groupNameBeforeEdit;
            groupNameBeforeEdit = null;
            if (g == null || old == null) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                string name = (g.Name ?? "").Trim();
                if (name.Length == 0)
                {
                    g.Name = old;   // không cho tên rỗng
                    return;
                }
                var other = ProgramModel.Vision.FindGroup(name);
                if (other != null && other != g)
                {
                    System.Windows.MessageBox.Show("A group with this name already exists.", "Rename group", MessageBoxButton.OK, MessageBoxImage.Warning);
                    g.Name = old;
                    return;
                }
                g.Name = name;
                if (name != old)
                {
                    foreach (var st in ProgramModel.TestSteps)
                        if (Model.StepCmd.Canonical(st.Cmd) == Model.StepCmd.Vision && string.Equals(st.Target, old, StringComparison.OrdinalIgnoreCase)) st.Target = name;
                }
                try { mainWindow?.sequencePage?.RefreshGroupNames(); } catch (Exception) { }
            }));
        }

        private void GroupAdd_Click(object sender, RoutedEventArgs e)
        {
            if (ProgramModel == null) return;
            var g = ProgramModel.Vision.AddGroup("");
            // Kế thừa HSV / bán kính / ngưỡng của group đang chọn cho đỡ chỉnh lại
            var cur = ProgramModel.Vision.SelectedGroupLED;
            if (cur != null)
            {
                g.RoiRadius = cur.RoiRadius;
                g.ContourArea = cur.ContourArea;
                g.HSV = cur.HSV.Clone();
            }
            RefreshGroupList();
            SelectGroup(g);
            // Mở luôn ô Name để đặt tên
            try
            {
                groupGrid.CurrentCell = new DataGridCellInfo(g, groupGrid.Columns[0]);
                groupGrid.BeginEdit();
            }
            catch (Exception) { }
        }

        private void GroupDelete_Click(object sender, RoutedEventArgs e)
        {
            var g = ProgramModel?.Vision.SelectedGroupLED;
            if (g == null) return;
            string msg = "Delete group \"" + g.Name + "\"" + (g.Colection.Count > 0 ? " and its " + g.Colection.Count + " ROI(s)?" : "?");
            if (System.Windows.MessageBox.Show(msg, "Delete group", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            builder.SelectedVisionObject = g;
            builder.ClearSelectedGroup();
            ProgramModel.Vision.Groups.Remove(g);
            RefreshGroupList();
            SelectGroup(null);
        }

        private void GroupNone_Click(object sender, RoutedEventArgs e)
        {
            SelectGroup(null);
        }

        private void UpdateSettings(string checkBoxName)
        {
            ResetPersistCounters();
            if (ProgramModel.Vision.SelectedGroupLED == null)
            {
                return;
            }



            cntSizeSlider.Value = ProgramModel.Vision.SelectedGroupLED.ContourArea;
            radiusSlider.Value = ProgramModel.Vision.SelectedGroupLED.RoiRadius;
            HueMin.Text = ProgramModel.Vision.SelectedGroupLED.HSV.HUE.Min.ToString();
            HueMax.Text = ProgramModel.Vision.SelectedGroupLED.HSV.HUE.Max.ToString();
            SatMin.Text = ProgramModel.Vision.SelectedGroupLED.HSV.SAT.Min.ToString();
            SatMax.Text = ProgramModel.Vision.SelectedGroupLED.HSV.SAT.Max.ToString();
            ValueMin.Text = ProgramModel.Vision.SelectedGroupLED.HSV.VAL.Min.ToString();
            ValueMax.Text = ProgramModel.Vision.SelectedGroupLED.HSV.VAL.Max.ToString();




        }
        private void cntRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ResetPersistCounters();
            builder.RoiRadius = (int)radiusSlider.Value;

            if (ProgramModel.Vision.SelectedGroupLED != null)
            {
                ProgramModel.Vision.SelectedGroupLED.RoiRadius = (int)radiusSlider.Value;


                foreach (var led in ProgramModel.Vision.SelectedGroupLED.Colection)
                {
                    led.RoiRadius = (int)radiusSlider.Value;
                    led.ResultFinal = SingleLED.RESULT.UNKNOWN;
                }
            }
        }

        private bool isDraggingMultipleLeds = false;

        public bool IsDraggingMultipleLeds
        {
            get
            {
                return isDraggingMultipleLeds;
            }

            set
            {
                isDraggingMultipleLeds = value;
                builder.IsDraggingMultipleLeds = value;
                if (value)
                {
                    GroupSelectionBtn.Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#323F4E"));
                    GroupSelectionIcon.Foreground = Brushes.White;

                }
                else
                {
                    GroupSelectionBtn.Background = Brushes.White;
                    GroupSelectionIcon.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#323F4E"));

                }

            }
        }


        private void GroupSelection_Click(object sender, RoutedEventArgs e)
        {
            IsDraggingMultipleLeds = !IsDraggingMultipleLeds;

        }



        private void Histogram_Analyzing_Click(object sender, RoutedEventArgs e)
        {
            HistogramAnalyzing histogramAnalyzing = new HistogramAnalyzing(this);
            histogramAnalyzing.Show();
        }







        private void MaintainState_Uncheck(object sender, RoutedEventArgs e)
        {
            if (ProgramModel.Vision.SelectedGroupLED == null) return;
            if (ProgramModel.Vision.SelectedGroupLED.MaintainState == null) return;
            ProgramModel.Vision.SelectedGroupLED.MaintainState = false;

        }

        private void MaintainState_Check(object sender, RoutedEventArgs e)
        {
            if (ProgramModel.Vision.SelectedGroupLED == null) return;
            if (ProgramModel.Vision.SelectedGroupLED.MaintainState == null) return;
            ProgramModel.Vision.SelectedGroupLED.MaintainState = true;
        }

        // Gửi chặt + cập nhật icon COM trên thanh top ngay (không chờ timer 1 s)
        private void SendControlStrict()
        {
            VisionTest.Device.SendControlStrict();
            try { mainWindow?.UpdateConnectionStatus(); } catch (Exception) { }
        }

        // Nút RL1..RL5: đảo trạng thái relay tương ứng rồi gửi cả gói lệnh (gửi chặt như Power / Up / Down)
        private void Relay_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Primitives.ToggleButton;
            if (btn == null || VisionTest?.Device == null) return;
            int idx;
            if (!int.TryParse(btn.Tag as string, out idx) || idx < 0 || idx >= VisionTest.Device.Relay.Length) return;

            bool ok = VisionTest.Device.SetRelay(idx, btn.IsChecked == true);   // một frame cho đúng relay này
            try { mainWindow?.UpdateConnectionStatus(); } catch (Exception) { }

            // Gửi không được (COM chưa nối / rớt) → trả nút về trạng thái cũ để không hiển thị sai
            if (!ok)
            {
                VisionTest.Device.Relay[idx] = false;
                btn.IsChecked = false;
            }
        }

        public void POWER_Btn_Click(object sender, RoutedEventArgs e)
        {
            VisionTest.Device.Power = !VisionTest.Device.Power;
            SendControlStrict();

            if (ProgramModel.Vision.SelectedGroupLED != null)
            {
                ProgramModel.Vision.SelectedGroupLED.MaintainState = true;

            }

        }


        private void Cancel_Click(object sender, RoutedEventArgs e)
        {

        }


        private void UpdateHistogram()
        {
            try
            {
                rois.Clear();

                originalImage = CameraSetting.Instance.LastMatFrame.Clone(); // Store original for ROI display

                var scale = originalImage.Width / this.cameraViewer.Width;

                // 3 bộ số liệu / 3 đường đồ thị:
                //   vòng đứt  / tím  = ROI ĐANG chọn
                //   vòng liền / xanh = các ROI CHƯA chọn của nhóm
                //   "All"     / cam  = tất cả ROI của nhóm
                var allRois = new List<(Point center, double radius)>();
                if (this.ProgramModel.Vision.SelectedGroupLED != null)
                {
                    foreach (var led in this.ProgramModel.Vision.SelectedGroupLED.Colection)
                    {
                        var item = (new Point(led.RoiPoint.X * scale, led.RoiPoint.Y * scale), led.RoiRadius * scale);
                        allRois.Add(item);
                        if (ReferenceEquals(led, builder.selectedLed)) continue;
                        rois.Add(item);
                    }
                }


                Mat hsv = new Mat();
                Cv2.CvtColor(originalImage, hsv, ColorConversionCodes.BGR2HSV);
                Mat mask = new Mat(hsv.Rows, hsv.Cols, MatType.CV_8UC1, Scalar.All(0));

                foreach (var roi in rois)
                {
                    Cv2.Circle(mask, (int)roi.center.X, (int)roi.center.Y, (int)roi.radius, Scalar.All(255), -1);
                }

                var channels = hsv.Split();
                var hHist = new Mat();
                var sHist = new Mat();
                var vHist = new Mat();

                Cv2.CalcHist(new[] { channels[0] }, new[] { 0 }, mask, hHist, 1, new[] { 180 }, new[] { new Rangef(0, 180) });
                Cv2.CalcHist(new[] { channels[1] }, new[] { 0 }, mask, sHist, 1, new[] { 256 }, new[] { new Rangef(0, 256) });
                Cv2.CalcHist(new[] { channels[2] }, new[] { 0 }, mask, vHist, 1, new[] { 256 }, new[] { new Rangef(0, 256) });

                var hRange = GetHistogramRange(hHist, 179, 179f);
                var sRange = GetHistogramRange(sHist, 255, 255f);
                var vRange = GetHistogramRange(vHist, 255, 255f);


                fullROIHue.Text =   $"{hRange.min:000} ~ {hRange.max:000}";
                fullROISat.Text =   $"{sRange.min:000} ~ {sRange.max:000}";
                fullROIValue.Text = $"{vRange.min:000} ~ {vRange.max:000}";

                // Series được tạo MỘT LẦN (EnsureHistSeries); mỗi tick chỉ đổi dữ liệu → đồ thị không chớp
                EnsureHistSeries();
                UpdateSeriesValues(hUnselSeries, hHist);
                UpdateSeriesValues(sUnselSeries, sHist);
                UpdateSeriesValues(vUnselSeries, vHist);

                // ----- All: tất cả ROI của nhóm (màu cam) -----
                {
                    Mat maskAll = new Mat(hsv.Rows, hsv.Cols, MatType.CV_8UC1, Scalar.All(0));
                    foreach (var roi in allRois)
                    {
                        Cv2.Circle(maskAll, (int)roi.center.X, (int)roi.center.Y, (int)roi.radius, Scalar.All(255), -1);
                    }
                    var hHistAll = new Mat();
                    var sHistAll = new Mat();
                    var vHistAll = new Mat();
                    Cv2.CalcHist(new[] { channels[0] }, new[] { 0 }, maskAll, hHistAll, 1, new[] { 180 }, new[] { new Rangef(0, 180) });
                    Cv2.CalcHist(new[] { channels[1] }, new[] { 0 }, maskAll, sHistAll, 1, new[] { 256 }, new[] { new Rangef(0, 256) });
                    Cv2.CalcHist(new[] { channels[2] }, new[] { 0 }, maskAll, vHistAll, 1, new[] { 256 }, new[] { new Rangef(0, 256) });

                    var hRangeAll = GetHistogramRange(hHistAll, 179, 179f);
                    var sRangeAll = GetHistogramRange(sHistAll, 255, 255f);
                    var vRangeAll = GetHistogramRange(vHistAll, 255, 255f);
                    allROIHue.Text = $"{hRangeAll.min:000} ~ {hRangeAll.max:000}";
                    allROISat.Text = $"{sRangeAll.min:000} ~ {sRangeAll.max:000}";
                    allROIValue.Text = $"{vRangeAll.min:000} ~ {vRangeAll.max:000}";

                    // All chỉ hiện số liệu min ~ max, không vẽ lên đồ thị
                }

                if (builder.selectedLed != null)
                {

                    Mat mask_ = new Mat(hsv.Rows, hsv.Cols, MatType.CV_8UC1, Scalar.All(0));

                    var x = builder.selectedLed.RoiPoint.X * scale;
                    var y = builder.selectedLed.RoiPoint.Y * scale;
                    var r = builder.selectedLed.RoiRadius * scale;
                    Cv2.Circle(mask_, (int)x, (int)y, (int)r, Scalar.All(255), -1);

                    var hHist_ = new Mat();
                    var sHist_ = new Mat();
                    var vHist_ = new Mat();



                    Cv2.CalcHist(new[] { channels[0] }, new[] { 0 }, mask_, hHist_, 1, new[] { 180 }, new[] { new Rangef(0, 180) });
                    Cv2.CalcHist(new[] { channels[1] }, new[] { 0 }, mask_, sHist_, 1, new[] { 256 }, new[] { new Rangef(0, 256) });
                    Cv2.CalcHist(new[] { channels[2] }, new[] { 0 }, mask_, vHist_, 1, new[] { 256 }, new[] { new Rangef(0, 256) });

                    var hRange_ = GetHistogramRange(hHist_, 179, 179f);
                    var sRange_ = GetHistogramRange(sHist_, 255, 255f);
                    var vRange_ = GetHistogramRange(vHist_, 255, 255f);


                    selectedROIHue.Text = $"{hRange_.min:000} ~ {hRange_.max:000}";
                    selectedROISat.Text = $"{sRange_.min:000} ~ {sRange_.max:000}";
                    selectedROIValue.Text = $"{vRange_.min:000} ~ {vRange_.max:000}";



                    UpdateSeriesValues(hSelSeries, hHist_);
                    UpdateSeriesValues(sSelSeries, sHist_);
                    UpdateSeriesValues(vSelSeries, vHist_);
                    hSelSeries.Visibility = Visibility.Visible;
                    sSelSeries.Visibility = Visibility.Visible;
                    vSelSeries.Visibility = Visibility.Visible;
                }
                else
                {
                    hSelSeries.Visibility = Visibility.Collapsed;
                    sSelSeries.Visibility = Visibility.Collapsed;
                    vSelSeries.Visibility = Visibility.Collapsed;
                }






            }
            catch
            {

            }


        }

        // 6 series cố định của 3 đồ thị H/S/V: Unselected (xanh dương), Selected (cam). All chỉ hiện số, không vẽ.
        private LineSeries hUnselSeries, sUnselSeries, vUnselSeries;
        private LineSeries hSelSeries, sSelSeries, vSelSeries;
        private bool histSeriesReady = false;

        private static LineSeries MakeSeries(System.Windows.Media.Brush stroke, int bins, string title)
        {
            var values = new ChartValues<double>();
            for (int i = 0; i < bins; i++) values.Add(0);
            return new LineSeries
            {
                Title = title,
                Values = values,
                PointGeometry = null,
                Stroke = stroke,
                Fill = Brushes.Transparent,
                LineSmoothness = 0,
                StrokeThickness = 1.5,
            };
        }

        private void EnsureHistSeries()
        {
            if (histSeriesReady) return;
            var blue = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));   // Unselected: xanh VS Code
            var orange = new SolidColorBrush(Color.FromRgb(0xF5, 0xA6, 0x23)); // Selected: amber

            hUnselSeries = MakeSeries(blue, 180, "Unselected");
            sUnselSeries = MakeSeries(blue, 256, "Unselected");
            vUnselSeries = MakeSeries(blue, 256, "Unselected");
            hSelSeries = MakeSeries(orange, 180, "Selected");
            sSelSeries = MakeSeries(orange, 256, "Selected");
            vSelSeries = MakeSeries(orange, 256, "Selected");

            HueChart.Series.Clear();
            SatChart.Series.Clear();
            ValueChart.Series.Clear();
            HueChart.Series.Add(hUnselSeries); HueChart.Series.Add(hSelSeries);
            SatChart.Series.Add(sUnselSeries); SatChart.Series.Add(sSelSeries);
            ValueChart.Series.Add(vUnselSeries); ValueChart.Series.Add(vSelSeries);
            histSeriesReady = true;
        }

        // Ghi giá trị histogram vào ChartValues có sẵn (không tạo series / collection mới → không chớp)
        private static void UpdateSeriesValues(LineSeries series, Mat hist)
        {
            var values = series.Values as ChartValues<double>;
            if (values == null) return;
            int n = Math.Min(values.Count, hist.Rows);
            for (int i = 0; i < n; i++)
            {
                double v = hist.Get<float>(i);
                if (values[i] != v) values[i] = v;
            }
        }

        private ChartValues<double> MatToList(Mat hist)
        {
            var list = new ChartValues<double>();
            for (int i = 0; i < hist.Rows; i++)
                list.Add(hist.Get<float>(i));
            return list;
        }


        private System.Windows.Controls.TextBox textBoxSelected;

        private List<System.Windows.Controls.TextBox> listTextBox;

        private void TextBlockColor_Click(object sender, MouseButtonEventArgs e)
        {
            foreach (var txtbox in listTextBox)
            {
                txtbox.BorderThickness = new Thickness(1, 1, 1, 1);

            }

            textBoxSelected = sender as System.Windows.Controls.TextBox;
            textBoxSelected.BorderThickness = new Thickness(2, 2, 2, 2);

            ApplyRange_Click(null, null);

        }

        private void Chart_DataClick(object sender, ChartPoint chartPoint)
        {
            if (chartPoint == null) return;
            var chart = chartPoint.ChartView as CartesianChart;


            if (textBoxSelected == null) return;

            string[] keys = { "Hue", "Sat", "Value" };

            foreach (var key in keys)
            {
                if (chart.Name.Contains(key) && textBoxSelected.Name.Contains(key))
                {
                    textBoxSelected.Text = chartPoint.X.ToString();
                    break;
                }
            }



        }
        private void Chart_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var chart = (CartesianChart)sender;

            // Get mouse position relative to the chart
            var mousePosition = e.GetPosition(chart);

            // Convert mouse position to chart values (X, Y)
            var chartPoint = chart.ConvertToChartValues(mousePosition);

            // Get the X value
            double xValue = Math.Round(chartPoint.X);


            if (textBoxSelected == null) return;

            string[] keys = { "Hue", "Sat", "Value" };

            foreach (var key in keys)
            {
                if (chart.Name.Contains(key) && textBoxSelected.Name.Contains(key))
                {
                    textBoxSelected.Text = xValue.ToString();
                    break;
                }
            }

            ApplyRange_Click(null, null);
        }
        private void ApplyRange_Click(object sender, RoutedEventArgs e)
        {
            ResetPersistCounters();
            double hueMax = double.Parse(HueMax.Text);
            double hueMin = double.Parse(HueMin.Text);
            double hueMean = Math.Floor((hueMax + hueMin) / 2);
            double hueTolerance = Math.Ceiling((hueMax - hueMin) / 2);

            double satMax = double.Parse(SatMax.Text);
            double satMin = double.Parse(SatMin.Text);
            double satMean = Math.Floor((satMax + satMin) / 2);
            double satTolerance = Math.Ceiling((satMax - satMin) / 2);


            double valueMax = double.Parse(ValueMax.Text);
            double valueMin = double.Parse(ValueMin.Text);
            double valueMean = Math.Floor((valueMax + valueMin) / 2);
            double valueTolerance = Math.Ceiling((valueMax - valueMin) / 2);

            GroupLED groupLED = this.ProgramModel.Vision.SelectedGroupLED;

            if (groupLED != null)
            {


                groupLED.HSV.HUE.Max = hueMax;
                groupLED.HSV.HUE.Min = hueMin;
                groupLED.HSV.SAT.Max = satMax;
                groupLED.HSV.SAT.Min = satMin;
                groupLED.HSV.VAL.Max = valueMax;
                groupLED.HSV.VAL.Min = valueMin;
            }

            this.colorpickerMain.Color.HSV_H = hueMean * 360 / 179;
            this.colorpickerMain.Color.HSV_S = satMean * 100 / 255;
            this.colorpickerMain.Color.HSV_V = valueMean * 100 / 255;

            if (groupLED != null)
                foreach (var item in groupLED.Colection)
                {
                    item.ResultFinal = SingleLED.RESULT.UNKNOWN;
                }


        }



        private Mat originalImage;
        private Mat displayImage;
        private List<(Point center, double radius)> rois = new List<(Point center, double radius)>();
        private BitmapSource imageSource;


        public void HistogramAnalyzing()
        {
            HueChart.Series = new LiveCharts.SeriesCollection();
            SatChart.Series = new LiveCharts.SeriesCollection();
            ValueChart.Series = new LiveCharts.SeriesCollection();




            HueChart.AxisY.Clear();
            HueChart.AxisY.Add(new Axis
            {
                MinValue = 0,
                MaxValue = 500
            });
            SatChart.AxisY.Clear();
            SatChart.AxisY.Add(new Axis
            {
                MinValue = 0,
                MaxValue = 500
            });
            ValueChart.AxisY.Clear();
            ValueChart.AxisY.Add(new Axis
            {
                MinValue = 0,
                MaxValue = 500
            });

            listTextBox = new List<TextBox> { SatMax, SatMin, HueMin, HueMax, ValueMax, ValueMin };

            if (this.ProgramModel.Vision.SelectedGroupLED != null)
            {
                HueMin.Text = this.ProgramModel.Vision.SelectedGroupLED.HSV.HUE.Min.ToString();
                HueMax.Text = this.ProgramModel.Vision.SelectedGroupLED.HSV.HUE.Max.ToString();
                SatMin.Text = this.ProgramModel.Vision.SelectedGroupLED.HSV.SAT.Min.ToString();
                SatMax.Text = this.ProgramModel.Vision.SelectedGroupLED.HSV.SAT.Max.ToString();
                ValueMin.Text = this.ProgramModel.Vision.SelectedGroupLED.HSV.VAL.Min.ToString();
                ValueMax.Text = this.ProgramModel.Vision.SelectedGroupLED.HSV.VAL.Max.ToString();
            }


            selectedROIHue.Text = $"----~----";
            selectedROISat.Text = $"----~----";
            selectedROIValue.Text = $"----~----";

            //UpdateHistogram();


        }

        public async void DOWN_Btn_Click(object sender, RoutedEventArgs e)
        {
            VisionTest.Device.CylinderDown = true;
            VisionTest.Device.CylinderUp = false;
            SendControlStrict();

            await Task.Delay(1000);

            VisionTest.Device.CylinderDown = false;
            VisionTest.Device.CylinderUp = false;
            SendControlStrict();


            if (ProgramModel.Vision.SelectedGroupLED != null)
            {
                ProgramModel.Vision.SelectedGroupLED.MaintainState = true;

            }
        }

        public async void UP_Btn_Click(object sender, RoutedEventArgs e)
        {
            VisionTest.Device.CylinderDown = false;
            VisionTest.Device.CylinderUp = true;
            SendControlStrict();


            await Task.Delay(1000);

            VisionTest.Device.CylinderDown = false;
            VisionTest.Device.CylinderUp = false;

            SendControlStrict();

            if (ProgramModel.Vision.SelectedGroupLED != null)
            {
                ProgramModel.Vision.SelectedGroupLED.MaintainState = true;

            }
        }
    }
}