using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace LEDVision.Camera
{
    public class Vision : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        public GroupLED SelectedGroupLED = null;

        public void EnableMouseEvent()
        {
            foreach (var led in FourLED.Colection)
            {
                led.EnableMouseEvent();
            }
            foreach (var led in SevenSEG.Colection)
            {
                led.EnableMouseEvent();
            }
            foreach (var led in DecimalPoint.Colection)
            {
                led.EnableMouseEvent();
            }
        }

        public void DisableMouseEvent()
        {
            foreach (var led in FourLED.Colection)
            {
                led.DisableMouseEvent();
            }
            foreach (var led in SevenSEG.Colection)
            {
                led.DisableMouseEvent();
            }
            foreach (var led in DecimalPoint.Colection)
            {
                led.DisableMouseEvent();
            }
        }


        public GroupLED FourLED { get; set; } = new GroupLED();

        public GroupLED SevenSEG { get; set; } = new GroupLED();

        public GroupLED DecimalPoint { get; set; } = new GroupLED();


        private System.Windows.Rect mainCanvasSize;

        [JsonIgnore]
        public System.Windows.Rect MainCanvasSize
        {
            get
            {
                return mainCanvasSize;
            }

            set
            {
                if (value != null)
                {
                    mainCanvasSize = value;
                }
            }
        }

        private Canvas mainCanvas;

        [JsonIgnore]
        public Canvas MainCanvas
        {
            get
            {
                return mainCanvas;
            }
            set
            {
                if (value != null)
                {
                    mainCanvas = value;
                    MainCanvasSize = new System.Windows.Rect()
                    {
                        X = 0,
                        Y = 0,
                        Width = value.ActualWidth,
                        Height = value.ActualHeight,
                    };
                }
            }
        }


        public void SetParentCanvas(Canvas canvas)
        {
            foreach (var led in FourLED.Colection)
            {
                led.SetParentCanvas(canvas);
            }
            foreach (var led in SevenSEG.Colection)
            {
                led.SetParentCanvas(canvas);
            }
            foreach (var led in DecimalPoint.Colection)
            {
                led.SetParentCanvas(canvas);
            }
        }

        public BitmapSource ObtainHSVAdjustedFrame(Mat frame, Canvas canvas)
        {

            if (frame == null || frame.Empty())
                return null;

            if (SelectedGroupLED == null) return null;

            Mat circleMask = new Mat(frame.Size(), MatType.CV_8UC3, new Scalar(0, 0, 0));
            double scaleX = (canvas.Width / frame.Width);
            double scaleY = (canvas.Height / frame.Height);


            for (int i = 0; i < SelectedGroupLED.Colection.Count; i++)
            {
                int centerX = (int)(SelectedGroupLED.Colection[i].RoiPoint.X / scaleX);
                int centerY = (int)(SelectedGroupLED.Colection[i].RoiPoint.Y / scaleY);
                int radius = (int)(SelectedGroupLED.Colection[i].RoiRadius / scaleX);

                Cv2.Circle(circleMask, new Point(centerX, centerY), radius, Scalar.White, -1);
            }

            Cv2.BitwiseAnd(frame, circleMask, frame);

            // Convert BGR to HSV
            var hsvFrame = new Mat();
            var mask = new Mat();


            Cv2.CvtColor(frame, hsvFrame, ColorConversionCodes.BGR2HSV);

            var lowerBound = new Scalar(SelectedGroupLED.HSV.HUE.Min, SelectedGroupLED.HSV.SAT.Min, SelectedGroupLED.HSV.VAL.Min);
            var upperBound = new Scalar(SelectedGroupLED.HSV.HUE.Max, SelectedGroupLED.HSV.SAT.Max, SelectedGroupLED.HSV.VAL.Max);
            Cv2.InRange(hsvFrame, lowerBound, upperBound, mask);


            // Apply the mask to the original frame
            var result = new Mat();

            Cv2.BitwiseAnd(frame, frame, result, mask);

            var finalResult = BitmapSourceConverter.ToBitmapSource(result);

            // Clean up
            hsvFrame.Dispose();
            mask.Dispose();

            return finalResult;
        }
        public int Inspect(Mat frame)
        {
            foreach (var led in SevenSEG.Colection)
            {
                led.Roi.Stroke = Brushes.Transparent;
                led.Roi.StrokeDashArray = null;

            }
            foreach (var led in FourLED.Colection)
            {
                led.Roi.Stroke = Brushes.Transparent;
                led.Roi.StrokeDashArray = null;

            }
            foreach (var led in DecimalPoint.Colection)
            {
                led.Roi.Stroke = Brushes.Transparent;
                led.Roi.StrokeDashArray = null;

            }

            if (SelectedGroupLED!=null)
            {
                string finalValue = string.Empty;
             
                foreach (var led in SelectedGroupLED.Colection)
                {
                    try
                    {
                        string output = led.CheckBlueArea(frame, SelectedGroupLED);
                        finalValue = output + finalValue;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }

                //Console.WriteLine(finalValue);

                if (finalValue.Contains("0"))
                {
                    return 1;
                }
            }
       
            return 0;
        }

        // Xóa bộ đếm persist của mọi ROI
        public void ResetPersistAll()
        {
            foreach (var led in FourLED.Colection) led.ResetPersist();
            foreach (var led in SevenSEG.Colection) led.ResetPersist();
            foreach (var led in DecimalPoint.Colection) led.ResetPersist();
        }

        public (List<bool>, List<bool>, List<bool>) InspectAll()
        {
            List<bool> ledResults = new List<bool>();
            List<bool> segmentResults = new List<bool>();
            List<bool> dpResults = new List<bool>();

            // Persist ở Auto: lấy mẫu 100 ms → đổi ms ra số khung; bắt đầu test mới thì xóa bộ đếm.
            // Các mẫu đầu (giai đoạn "ổn định", chưa đủ N khung) KHÔNG chấm điểm, nếu không test luôn NG.
            const int sampleMs = 100;
            SingleLED.PersistFrames = SingleLED.FramesFor(sampleMs);
            ResetPersistAll();
            int settle = (SingleLED.PersistEnabled && SingleLED.PersistFrames > 1) ? SingleLED.PersistFrames : 0;
            int maxSettle = (int)(2000 / sampleMs) - 5;          // luôn chừa ít nhất 5 mẫu để chấm
            if (settle > maxSettle) settle = maxSettle;
            int sampleIndex = 0;

            try
            {
                foreach (var led in DecimalPoint.Colection)
                {
                    led.ResultFinal = SingleLED.RESULT.UNKNOWN;
                }
                foreach (var led in SevenSEG.Colection)
                {
                    led.ResultFinal = SingleLED.RESULT.UNKNOWN;
                }
                foreach (var led in FourLED.Colection)
                {
                    led.ResultFinal = SingleLED.RESULT.UNKNOWN;
                }

                DecimalPoint.MaintainState = true;
                SevenSEG.MaintainState = true;
                FourLED.MaintainState = true;

               

                DateTime startTime = DateTime.Now;
                TimeSpan timeout = TimeSpan.FromMilliseconds(2000);

                while (DateTime.Now - startTime < timeout)
                {
                    if (VisionTest.CancelRequested) break;   // hủy test → dừng lấy mẫu ngay
                    if (CameraSetting.Instance.LastMatFrame != null)
                    {
                        Mat frame = CameraSetting.Instance.LastMatFrame.Clone();
                        bool score = sampleIndex >= settle;   // mẫu trong giai đoạn ổn định chỉ nuôi bộ đếm

                        // Process DecimalPoint collection
                        foreach (var led in DecimalPoint.Colection)
                        {
                            string output = led.CheckBlueArea(frame, DecimalPoint);
                            if (score) dpResults.Add(output == "1");
                        }

                        // Process SevenSEG collection
                        foreach (var led in SevenSEG.Colection)
                        {
                            string output = led.CheckBlueArea(frame, SevenSEG);
                            if (score) segmentResults.Add(output == "1");
                        }

                        // Process FourLED collection
                        foreach (var led in FourLED.Colection)
                        {
                            string output = led.CheckBlueArea(frame, FourLED);
                            if (score) ledResults.Add(output == "1");
                        }
                        frame.Dispose();
                        sampleIndex++;
                    }
                    Task.Delay(sampleMs).Wait();
                }            
            }
            catch(Exception ex)
            {
            }

            return (ledResults, segmentResults, dpResults);

        }
    }
}