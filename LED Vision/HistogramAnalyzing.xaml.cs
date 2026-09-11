using LiveCharts.Wpf;
using LiveCharts;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


using Point = System.Windows.Point;
using Window = System.Windows.Window;
using LEDVision.Camera;
using System.Timers;
namespace LEDVision
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class HistogramAnalyzing : Window
    {
        private Mat originalImage;
        private Mat displayImage;
        private List<(Point center, double radius)> rois = new List<(Point center, double radius)>();
        private BitmapSource imageSource;
        private VisionPage visionPage;

        private System.Timers.Timer obtainFrameTimer = new System.Timers.Timer()
        {
            Interval = 1500
        };


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
                    UpdateHistogram();
                }));
            }

        }
        public HistogramAnalyzing(VisionPage vsPage)
        {
            InitializeComponent();

            obtainFrameTimer.Elapsed += ObtainFrameTimer_Elapsed;
            obtainFrameTimer.Start();
            visionPage = vsPage;


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

            if (visionPage.ProgramModel.Vision.SelectedGroupLED != null)
            {
                HueMin.Text = visionPage.ProgramModel.Vision.SelectedGroupLED.HSV.HUE.Min.ToString();
                HueMax.Text = visionPage.ProgramModel.Vision.SelectedGroupLED.HSV.HUE.Max.ToString();
                SatMin.Text = visionPage.ProgramModel.Vision.SelectedGroupLED.HSV.SAT.Min.ToString();
                SatMax.Text = visionPage.ProgramModel.Vision.SelectedGroupLED.HSV.SAT.Max.ToString();
                ValueMin.Text = visionPage.ProgramModel.Vision.SelectedGroupLED.HSV.VAL.Min.ToString();
                ValueMax.Text = visionPage.ProgramModel.Vision.SelectedGroupLED.HSV.VAL.Max.ToString();
            }

            UpdateHistogram();


        }
      
        private void UpdateHistogram()
        {
            rois.Clear();

            originalImage = CameraSetting.Instance.LastMatFrame.Clone(); // Store original for ROI display

            var scale = originalImage.Width / visionPage.cameraViewer.Width;

            if (visionPage.ProgramModel.Vision.SelectedGroupLED != null)
            {
                foreach (var led in visionPage.ProgramModel.Vision.SelectedGroupLED.Colection)
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

            Dispatcher.Invoke(() =>
            {

                HueChart.Series = new LiveCharts.SeriesCollection { new LineSeries { Values = MatToList(hHist), PointGeometry = null } };
                SatChart.Series = new LiveCharts.SeriesCollection { new LineSeries { Values = MatToList(sHist), PointGeometry = null } };
                ValueChart.Series = new LiveCharts.SeriesCollection { new LineSeries { Values = MatToList(vHist), PointGeometry = null } };
            });


          

        }

        private ChartValues<double> MatToList(Mat hist)
        {
            var list = new ChartValues<double>();
            for (int i = 0; i < hist.Rows; i++)
                list.Add(hist.Get<float>(i));
            return list;
        }


        private TextBox textBoxSelected;

        private List<TextBox> listTextBox;

        private void TextBlockColor_Click(object sender, MouseButtonEventArgs e)
        {
            foreach (var txtbox in listTextBox)
            {
                txtbox.BorderThickness = new Thickness(1, 1, 1, 1);

            }

            textBoxSelected = sender as TextBox;
            textBoxSelected.BorderThickness = new Thickness(2, 2, 2, 2);
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

            GroupLED groupLED = visionPage.ProgramModel.Vision.SelectedGroupLED;

            if (groupLED != null)
            {
                groupLED.HSV.HUE.Min = hueMin;
                groupLED.HSV.SAT.Min = satMin;
                groupLED.HSV.VAL.Min = valueMin;

                groupLED.HSV.HUE.Max = hueMax;
                groupLED.HSV.SAT.Max = satMax;
                groupLED.HSV.VAL.Max = valueMax;
            }

            visionPage.colorpickerMain.Color.HSV_H = hueMean * 360 / 179;
            visionPage.colorpickerMain.Color.HSV_S = satMean * 100 / 255;
            visionPage.colorpickerMain.Color.HSV_V = valueMean * 100 / 255;


            foreach(var item in groupLED.Colection)
            {
                item.ResultFinal = SingleLED.RESULT.UNKNOWN;
            }


        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }


    }
}
