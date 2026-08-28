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
            persistFramesBox.Text = settingModel.SettingVal.PersistFrames.ToString();
            loadingPersist = false;

            Camera.SingleLED.PersistEnabled = settingModel.SettingVal.PersistEnabled;
            Camera.SingleLED.PersistFrames = settingModel.SettingVal.PersistFrames;
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

            if (int.TryParse(persistFramesBox.Text, out int frames) && frames >= 1)
            {
                settingModel.SettingVal.PersistFrames = frames;
            }
            else
            {
                persistFramesBox.Text = settingModel.SettingVal.PersistFrames.ToString();
            }

            Camera.SingleLED.PersistEnabled = settingModel.SettingVal.PersistEnabled;
            Camera.SingleLED.PersistFrames = settingModel.SettingVal.PersistFrames;

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

        private void ObtainFrameTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (CameraSetting.Instance.LastMatFrame != null)
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    EnsureSurfaceSized(CameraSetting.Instance.LastMatFrame);

                    adjustedImageViewer.Source = ProgramModel.Vision.ObtainHSVAdjustedFrame(CameraSetting.Instance.LastMatFrame.Clone(), mainCanvas);

                    try
                    {
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

        private CameraSettingValues boundCameraValues;

        // Bind danh sách thông số camera (Expander) vào bộ dùng chung; kéo slider là áp lên camera ngay.
        private void BindCameraSettings()
        {
            if (boundCameraValues != null)
                boundCameraValues.PropertyChanged -= CameraValues_PropertyChanged;

            boundCameraValues = CameraSetting.Instance.cameraSettingValues;
            cameraSettingsPanel.DataContext = boundCameraValues;
            boundCameraValues.PropertyChanged += CameraValues_PropertyChanged;
        }

        private void CameraValues_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            CameraSetting.Instance.SetParammeter(boundCameraValues);
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
        public void SaveModelBtn_Click(object sender, RoutedEventArgs e)
        {

            ProgramModel.CameraSettingValues = CameraSetting.Instance.cameraSettingValues;

            Microsoft.Win32.SaveFileDialog openFile = new Microsoft.Win32.SaveFileDialog();
            if (openFile.ShowDialog() == true)
            {
                try
                {
                    Utility.SaveModel(ProgramModel, openFile.FileName, openFile.SafeFileName);

                    CameraSetting.Instance.loadedCameraSettingValues = CameraSetting.Instance.cameraSettingValues.Clone();

                    mainWindow.autoPage.ProgramModel = ProgramModel;
                }
                catch (Exception)
                {
                }
            }
        }
        private void ColorChanged()
        {
            bool segmentChecked = segmentCheckBox.IsChecked.Value;
            bool decimalPointChecked = decimalPointCheckBox.IsChecked.Value;
            bool ledChecked = ledCheckBox.IsChecked.Value;

          
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
            if (ProgramModel.Vision.SelectedGroupLED != null)
            {
                ProgramModel.Vision.SelectedGroupLED.ContourArea = (int)cntSizeSlider.Value;

                foreach (var item in ProgramModel.Vision.SelectedGroupLED.Colection)
                {
                    item.ResultFinal = SingleLED.RESULT.UNKNOWN;
                }
            }
        }
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var chkBox = (System.Windows.Controls.CheckBox)sender;

            switch (chkBox.Name)
            {
                case "ledCheckBox":

                    defaultCheckBox.IsChecked = false;
                    segmentCheckBox.IsChecked = false;
                    decimalPointCheckBox.IsChecked = false;

                    ProgramModel.Vision.SelectedGroupLED = ProgramModel.Vision.FourLED;
                    builder.SelectedVisionObject = ProgramModel.Vision.FourLED;

                    UpdateSettings("ledCheckBox");
                    break;

                case "segmentCheckBox":

                    defaultCheckBox.IsChecked = false;
                    ledCheckBox.IsChecked = false;
                    decimalPointCheckBox.IsChecked = false;

                    ProgramModel.Vision.SelectedGroupLED = ProgramModel.Vision.SevenSEG;
                    builder.SelectedVisionObject = ProgramModel.Vision.SevenSEG;
                    UpdateSettings("segmentCheckBox");
                    break;
                case "decimalPointCheckBox":
                    defaultCheckBox.IsChecked = false;
                    segmentCheckBox.IsChecked = false;
                    ledCheckBox.IsChecked = false;
                    ProgramModel.Vision.SelectedGroupLED = ProgramModel.Vision.DecimalPoint;
                    builder.SelectedVisionObject = ProgramModel.Vision.DecimalPoint;
                    UpdateSettings("decimalPointCheckBox");
                    break;
                default:

                    segmentCheckBox.IsChecked = false;
                    ledCheckBox.IsChecked = false;
                    decimalPointCheckBox.IsChecked = false;
                    ProgramModel.Vision.SelectedGroupLED = null;
                    builder.SelectedVisionObject = null;

                    break;

            }
        }

        private void UpdateSettings(string checkBoxName)
        {
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

        public void POWER_Btn_Click(object sender, RoutedEventArgs e)
        {
            VisionTest.Device.Power = !VisionTest.Device.Power;
            VisionTest.Device.SendControl();

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

                if (this.ProgramModel.Vision.SelectedGroupLED != null)
                {
                    foreach (var led in this.ProgramModel.Vision.SelectedGroupLED.Colection)
                    {
                        rois.Add((new Point(led.RoiPoint.X * scale, led.RoiPoint.Y * scale), led.RoiRadius * scale));
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

                HueChart.Series.Clear();
                SatChart.Series.Clear();
                ValueChart.Series.Clear();

                HueChart.Series.Add(new LineSeries { Values = MatToList(hHist), PointGeometry = null   });
                SatChart.Series.Add(new LineSeries { Values = MatToList(sHist), PointGeometry = null   });
                ValueChart.Series.Add(new LineSeries { Values = MatToList(vHist), PointGeometry = null });

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



                    HueChart.Series.Add(new LineSeries { Values = MatToList(hHist_), PointGeometry = null, Stroke = new SolidColorBrush(Colors.Purple) });
                    SatChart.Series.Add(new LineSeries { Values = MatToList(sHist_), PointGeometry = null, Stroke = new SolidColorBrush(Colors.Purple) });
                    ValueChart.Series.Add(new LineSeries { Values = MatToList(vHist_), PointGeometry = null, Stroke = new SolidColorBrush(Colors.Purple) });
                   

                }






            }
            catch
            {

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

            GroupLED groupLED = null;
            if (this.segmentCheckBox.IsChecked.Value)
            {
                groupLED = this.ProgramModel.Vision.SevenSEG;

            }
            if (this.ledCheckBox.IsChecked.Value)
            {
                groupLED = this.ProgramModel.Vision.FourLED;
            }
            if (this.decimalPointCheckBox.IsChecked.Value)
            {
                groupLED = this.ProgramModel.Vision.DecimalPoint;
            }

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
            VisionTest.Device.SendControl();

            await Task.Delay(1000);

            VisionTest.Device.CylinderDown = false;
            VisionTest.Device.CylinderUp = false;
            VisionTest.Device.SendControl();


            if (ProgramModel.Vision.SelectedGroupLED != null)
            {
                ProgramModel.Vision.SelectedGroupLED.MaintainState = true;

            }
        }

        public async void UP_Btn_Click(object sender, RoutedEventArgs e)
        {
            VisionTest.Device.CylinderDown = false;
            VisionTest.Device.CylinderUp = true;
            VisionTest.Device.SendControl();


            await Task.Delay(1000);

            VisionTest.Device.CylinderDown = false;
            VisionTest.Device.CylinderUp = false;

            VisionTest.Device.SendControl();

            if (ProgramModel.Vision.SelectedGroupLED != null)
            {
                ProgramModel.Vision.SelectedGroupLED.MaintainState = true;

            }
        }
    }
}