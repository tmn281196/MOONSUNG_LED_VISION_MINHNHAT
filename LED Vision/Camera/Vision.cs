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


        [JsonIgnore]
        public GroupLED SelectedGroupLED = null;

        // ---- Danh sách group LED động: người dùng thêm / xóa / đặt tên tùy ý ----
        // Mỗi group có bộ HSV, bán kính ROI, ngưỡng diện tích riêng. Step VISION CHECK gọi group theo tên.
        public ObservableCollection<GroupLED> Groups { get; set; } = new ObservableCollection<GroupLED>();

        // Tương thích model cũ (3 group cố định). Chỉ có setter → không ghi ra file nữa; đọc file cũ thì
        // đổ vào Groups với tên cũ. Group rỗng của file cũ bị bỏ qua.
        public GroupLED FourLED { set { AddLegacy(value, "4-LED"); } }
        public GroupLED SevenSEG { set { AddLegacy(value, "7-Segment"); } }
        public GroupLED DecimalPoint { set { AddLegacy(value, "Decimal Point"); } }

        private void AddLegacy(GroupLED g, string name)
        {
            if (g == null || g.Colection == null || g.Colection.Count == 0) return;
            if (string.IsNullOrWhiteSpace(g.Name)) g.Name = name;
            Groups.Add(g);
        }

        // Mọi ROI của mọi group
        public IEnumerable<SingleLED> AllLeds()
        {
            foreach (var g in Groups)
                foreach (var led in g.Colection)
                    yield return led;
        }

        // Tìm group theo tên (không phân biệt hoa thường). Không có → null.
        public GroupLED FindGroup(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            name = name.Trim();
            foreach (var g in Groups)
                if (string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)) return g;
            return null;
        }

        // Tên chưa dùng: "Group 1", "Group 2", ...
        public string NextGroupName()
        {
            for (int i = 1; ; i++)
            {
                string n = "Group " + i;
                if (FindGroup(n) == null) return n;
            }
        }

        // Thêm group mới với tên (rỗng → tự đặt). Tên trùng → thêm hậu tố.
        public GroupLED AddGroup(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) name = NextGroupName();
            name = name.Trim();
            string baseName = name;
            for (int i = 2; FindGroup(name) != null; i++) name = baseName + " " + i;
            var g = new GroupLED { Name = name };
            Groups.Add(g);
            return g;
        }

        public void EnableMouseEvent()
        {
            foreach (var led in AllLeds()) led.EnableMouseEvent();
        }

        public void DisableMouseEvent()
        {
            foreach (var led in AllLeds()) led.DisableMouseEvent();
        }


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
            foreach (var led in AllLeds()) led.SetParentCanvas(canvas);
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
            foreach (var led in AllLeds())
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
            foreach (var led in AllLeds()) led.ResetPersist();
        }

        // Kết quả một lần VISION CHECK của một group
        public class GroupCheckResult
        {
            public bool Pass;      // mọi mẫu của mọi ROI đều sáng đúng màu
            public int LedCount;   // số ROI trong group
            public int NgLeds;     // số ROI có ít nhất một mẫu NG
            public int Samples;    // số mẫu đã chấm
        }

        // Lấy mẫu liên tục durationMs (chu kỳ 100 ms) và chấm mọi ROI của MỘT group. Dùng cho step VISION CHECK.
        // Persist: mẫu đầu (chưa đủ N khung) chỉ nuôi bộ đếm, không chấm. Hủy test → dừng ngay.
        public GroupCheckResult InspectGroup(GroupLED group, int durationMs)
        {
            var r = new GroupCheckResult();
            if (group == null) return r;
            r.LedCount = group.Colection.Count;
            if (durationMs < 200) durationMs = 200;

            const int sampleMs = 100;
            SingleLED.PersistFrames = SingleLED.FramesFor(sampleMs);
            foreach (var led in group.Colection)
            {
                led.ResetPersist();
                led.ResultFinal = SingleLED.RESULT.UNKNOWN;
            }
            group.MaintainState = true;

            int settle = (SingleLED.PersistEnabled && SingleLED.PersistFrames > 1) ? SingleLED.PersistFrames : 0;
            int maxSettle = (int)(durationMs / sampleMs) - 5;   // luôn chừa ít nhất 5 mẫu để chấm
            if (maxSettle < 0) maxSettle = 0;
            if (settle > maxSettle) settle = maxSettle;

            bool[] ledNg = new bool[group.Colection.Count];
            int sampleIndex = 0;
            try
            {
                DateTime startTime = DateTime.Now;
                TimeSpan timeout = TimeSpan.FromMilliseconds(durationMs);
                while (DateTime.Now - startTime < timeout)
                {
                    if (VisionTest.CancelRequested) break;
                    if (CameraSetting.Instance.LastMatFrame != null)
                    {
                        Mat frame = CameraSetting.Instance.LastMatFrame.Clone();
                        bool score = sampleIndex >= settle;
                        for (int i = 0; i < group.Colection.Count; i++)
                        {
                            string output = group.Colection[i].CheckBlueArea(frame, group);
                            if (score && output != "1") ledNg[i] = true;
                        }
                        frame.Dispose();
                        if (score) r.Samples++;
                        sampleIndex++;
                    }
                    Task.Delay(sampleMs).Wait();
                }
            }
            catch (Exception)
            {
            }

            r.NgLeds = ledNg.Count(x => x);
            r.Pass = r.Samples > 0 && r.NgLeds == 0;
            return r;
        }

        // Chấm MỌI group trong 2 s (cách cũ, giữ lại cho tương thích)
        public (List<bool>, List<bool>, List<bool>) InspectAll()
        {
            List<bool> ledResults = new List<bool>();
            List<bool> segmentResults = new List<bool>();
            List<bool> dpResults = new List<bool>();

            const int sampleMs = 100;
            SingleLED.PersistFrames = SingleLED.FramesFor(sampleMs);
            ResetPersistAll();
            int settle = (SingleLED.PersistEnabled && SingleLED.PersistFrames > 1) ? SingleLED.PersistFrames : 0;
            int maxSettle = (int)(2000 / sampleMs) - 5;
            if (settle > maxSettle) settle = maxSettle;
            int sampleIndex = 0;

            try
            {
                foreach (var g in Groups)
                {
                    foreach (var led in g.Colection) led.ResultFinal = SingleLED.RESULT.UNKNOWN;
                    g.MaintainState = true;
                }

                DateTime startTime = DateTime.Now;
                TimeSpan timeout = TimeSpan.FromMilliseconds(2000);

                while (DateTime.Now - startTime < timeout)
                {
                    if (VisionTest.CancelRequested) break;
                    if (CameraSetting.Instance.LastMatFrame != null)
                    {
                        Mat frame = CameraSetting.Instance.LastMatFrame.Clone();
                        bool score = sampleIndex >= settle;
                        foreach (var g in Groups)
                        {
                            foreach (var led in g.Colection)
                            {
                                string output = led.CheckBlueArea(frame, g);
                                if (score) ledResults.Add(output == "1");
                            }
                        }
                        frame.Dispose();
                        sampleIndex++;
                    }
                    Task.Delay(sampleMs).Wait();
                }
            }
            catch(Exception)
            {
            }

            return (ledResults, segmentResults, dpResults);

        }
    }
}
