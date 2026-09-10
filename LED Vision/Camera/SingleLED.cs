using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LEDVision.Camera
{

    public class SingleLED
    {
        public enum RESULT{
            UNKNOWN,
            OK,
            NG
        }
        public SingleLED()
        {
            ResultFinal = RESULT.UNKNOWN;
        }
        private System.Windows.Point startPoint = new System.Windows.Point();
        private double initialX, initialY;

        private int roiRadius = 20;
        public int RoiRadius
        {
            get { return roiRadius; }
            set
            {
                if (value != null)
                {
                    roiRadius = value;
                    SetEllipseRoi();
                }
            }
        }

        public RESULT ResultFinal { get; set; } = RESULT.UNKNOWN;

        // Cấu hình persist TOÀN CỤC (đồng bộ từ setting.json). Chống nhấp nháy kết quả PASS/NG.
        // Màu ROI khi PASS / mặc định: xanh LINE (#06C755), cùng màu popup PASS
        public static readonly System.Windows.Media.Brush PassBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x06, 0xC7, 0x55));

        public static bool PersistEnabled = false;
        public static int PersistFrames = 5;

        // Persist mức mask (AND): ảnh đếm "số khung sáng LIÊN TIẾP" của từng pixel (CV_32S, kích thước ROI).
        private Mat consecOnMat;


        private Canvas mainCanvas;

        [JsonIgnore]
        public Canvas MainCanvas
        {
            get { return mainCanvas; }
            set
            {
                if (value != null && value != mainCanvas)
                {
                    mainCanvas = value;
                }
            }
        }


        private Ellipse roi;

        [JsonIgnore]
        public Ellipse Roi
        {
            get { return roi; }
            set
            {
                if (value != null)
                {
                    roi = value;
                    roi.Stroke = PassBrush;
                    roi.StrokeThickness = 2;
                    roi.Fill = Brushes.Transparent;

                    EnableMouseEvent();

                }
            }
        }

        public void EnableMouseEvent()
        {
            if (roi != null)
            {
                roi.MouseDown += Roi_MouseDown;
                roi.MouseEnter += Roi_MouseEnter;
                roi.MouseMove += Roi_MouseMove;
                roi.MouseUp += Roi_MouseUp;
            }
        }

        public void DisableMouseEvent()
        {
            if (roi != null)
            {
                roi.MouseDown -= Roi_MouseDown;
                roi.MouseEnter -= Roi_MouseEnter;
                roi.MouseMove -= Roi_MouseMove;
                roi.MouseUp -= Roi_MouseUp;
            }
        }
        private void Roi_KeyDown(object sender, KeyEventArgs e)
        {
            Keyboard.Focus(Roi);
        }
        private void Roi_MouseEnter(object sender, MouseEventArgs e)
        {
            Roi.Cursor = Cursors.SizeAll;
        }

        private System.Windows.Point roiPoint;
        public System.Windows.Point RoiPoint
        {
            get { return roiPoint; }
            set
            {
                if (value != roiPoint)
                {
                    roiPoint = value;
                    SetEllipseRoi();
                }
            }
        }

        private void SetEllipseRoi()
        {
            if (Roi == null)
            {
                Roi = new Ellipse();
            }
            Roi.Focusable = true;
            Roi.Width = RoiRadius * 2;
            Roi.Height = RoiRadius * 2;
            Canvas.SetLeft(Roi, RoiPoint.X - RoiRadius);
            Canvas.SetTop(Roi, RoiPoint.Y - RoiRadius);
        }
        // Kéo ROI bằng chuột trái. Bắt chuột (CaptureMouse) khi bấm để con trỏ trượt ra ngoài vòng tròn
        // ROI vẫn đi theo; không phụ thuộc vào phần tử đang có focus bàn phím.
        private bool isDragging = false;

        public void Roi_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || mainCanvas == null) return;

            isDragging = true;
            Roi.Cursor = Cursors.SizeAll;
            Keyboard.Focus(Roi);
            startPoint = e.GetPosition(mainCanvas);
            initialX = Canvas.GetLeft(Roi);
            initialY = Canvas.GetTop(Roi);
            Roi.CaptureMouse();
        }

        public void Roi_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!isDragging || mainCanvas == null) return;
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                EndDrag();
                return;
            }

            System.Windows.Point currentPosition = e.GetPosition(mainCanvas);
            double offsetX = currentPosition.X - startPoint.X;
            double offsetY = currentPosition.Y - startPoint.Y;

            Canvas.SetLeft(Roi, initialX + offsetX);
            Canvas.SetTop(Roi, initialY + offsetY);
        }

        public void Roi_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (isDragging) EndDrag();
        }

        // Kết thúc kéo: ghi vị trí mới vào RoiPoint, thả chuột
        private void EndDrag()
        {
            isDragging = false;
            var x = Canvas.GetLeft(Roi);
            var y = Canvas.GetTop(Roi);
            RoiPoint = new System.Windows.Point(x + Roi.Width / 2, y + Roi.Height / 2);
            Roi.ReleaseMouseCapture();
            Roi.Cursor = Cursors.Arrow;
        }

        public void SetParentCanvas(Canvas mainCanvas)
        {
            MainCanvas = mainCanvas;
        }

        public bool IsPointInsideRectangle(Rectangle rectangle)
        {
            if (rectangle == null) return false;

            // Get rectangle position and size
            double left = Canvas.GetLeft(rectangle);
            double top = Canvas.GetTop(rectangle);
            double right = left + rectangle.Width;
            double bottom = top + rectangle.Height;

            // Check if the point is within the rectangle bounds
            return (this.RoiPoint.X >= left && this.RoiPoint.X <= right &&
                    this.RoiPoint.Y >= top && this.RoiPoint.Y <= bottom);
        }

        public bool IsPointInsideEllipse(System.Windows.Point point, Ellipse ellipse)
        {
            if (ellipse == null) return false;

            // Get ellipse position and size
            double left = Canvas.GetLeft(ellipse);
            double top = Canvas.GetTop(ellipse);
            double a = ellipse.Width / 2;  // Semi-major axis
            double b = ellipse.Height / 2; // Semi-minor axis

            // Compute the center of the ellipse
            double centerX = left + a;
            double centerY = top + b;

            // Check if the point satisfies the ellipse equation
            double normalizedX = (point.X - centerX) / a;
            double normalizedY = (point.Y - centerY) / b;

            return (normalizedX * normalizedX + normalizedY * normalizedY) <= 1;
        }

        public int CountWhitePixels(Mat mask)
        {
            if (mask == null || mask.Empty())
                return 0;

            int whitePixelCount = 0;

            // Iterate over each pixel in the mask
            for (int y = 0; y < mask.Rows; y++)
            {
                for (int x = 0; x < mask.Cols; x++)
                {
                    // Check if the pixel is white (value of 255)
                    if (mask.At<byte>(y, x) == 255)
                    {
                        whitePixelCount++;
                    }
                }
            }

            return whitePixelCount;
        }

        // Persist mức mask (AND): đếm pixel "sáng LIÊN TIẾP đủ N khung".
        // Mỗi khung: count += 1 ở mọi pixel, reset 0 nơi đang TẮT; pixel ON hiệu dụng nếu count >= n.
        private int CountPersistedWhite(Mat mask, int n)
        {
            if (mask == null || mask.Empty())
                return 0;

            // Cấp phát/tái cấp phát nếu kích thước ROI đổi (đổi bán kính). Khởi tạo = 0.
            if (consecOnMat == null || consecOnMat.Rows != mask.Rows || consecOnMat.Cols != mask.Cols)
            {
                consecOnMat?.Dispose();
                consecOnMat = new Mat(mask.Rows, mask.Cols, MatType.CV_32S, Scalar.All(0));
            }

            Cv2.Add(consecOnMat, Scalar.All(1), consecOnMat);   // count += 1 ở mọi pixel
            using (var off = new Mat())
            {
                Cv2.BitwiseNot(mask, off);                      // off != 0 nơi mask == 0 (LED tắt)
                consecOnMat.SetTo(Scalar.All(0), off);          // reset count = 0 nơi LED tắt
            }

            using (var eff = new Mat())
            {
                Cv2.Compare(consecOnMat, Scalar.All(n), eff, CmpType.GE); // eff = (count >= n)
                return Cv2.CountNonZero(eff);
            }
        }


        public string CheckBlueArea(Mat frame, GroupLED groupLED)
        {

            if (frame == null || frame.Empty())
                return "";


            double scaleX = frame.Width / MainCanvas.Width;
            double scaleY = frame.Height / MainCanvas.Height;

            // Scale circle ROI
            int cx = (int)(RoiPoint.X * scaleX);
            int cy = (int)(RoiPoint.Y * scaleY);
            int rx = (int)(Roi.Width * scaleX / 2); // Keep uniform scaling
            int ry = (int)(Roi.Height * scaleY / 2); // Keep uniform scaling

            // Ensure the circle is within bounds
            int x = Math.Max(0, cx - rx);
            int y = Math.Max(0, cy - ry);
            int width = 2 * rx;
            int height = 2 * ry;

            OpenCvSharp.Rect boundingBox = new OpenCvSharp.Rect(x, y, width, height);

            // Crop the bounding box region
            Mat croppedMat = new Mat(frame, boundingBox);

            // Create a circular mask
            Mat mask = new Mat(width, height, MatType.CV_8UC1, Scalar.Black);
            Cv2.Circle(mask, new OpenCvSharp.Point(rx, ry), (int)(rx), Scalar.White, -1); // Draw filled circle mask


            // Convert to HSV
            Mat hsvFrame = new Mat();
            Cv2.CvtColor(croppedMat, hsvFrame, ColorConversionCodes.BGR2HSV);

            // Apply color threshold
            Mat thresholdMask = new Mat();
            Cv2.InRange(hsvFrame, new Scalar(groupLED.HSV.HUE.Min, groupLED.HSV.SAT.Min, groupLED.HSV.VAL.Min), new Scalar(groupLED.HSV.HUE.Max, groupLED.HSV.SAT.Max, groupLED.HSV.VAL.Max), thresholdMask);

            // Apply circular mask
            Cv2.BitwiseAnd(thresholdMask, mask, thresholdMask);


            // Persist mức mask (AND): chỉ pixel sáng LIÊN TIẾP đủ N khung mới tính là sáng → nghiêm,
            // LED hễ rớt/dim 1 khung là bị loại → thiên về bắt NG.
            int effectiveCount;
            if (PersistEnabled && PersistFrames > 1)
            {
                effectiveCount = CountPersistedWhite(thresholdMask, PersistFrames);
            }
            else
            {
                effectiveCount = CountWhitePixels(thresholdMask);
            }

            bool result = effectiveCount > groupLED.ContourArea;
            ResultFinal = result ? RESULT.OK : RESULT.NG;


            try
            {
                if (Roi != null)
                {
             
                    // Check if we need to invoke (if not on UI thread)
                    if (Roi.Dispatcher.CheckAccess())
                        {
                            Roi.Stroke = result ? PassBrush : Brushes.Red;
                        }
                        else
                        {
                            // Invoke on UI thread
                            Roi.Dispatcher.Invoke(() =>
                            {
                                Roi.Stroke = result ? PassBrush : Brushes.Red;
                            });
                        }
                 
                  

                }
                return result ? "1" : "0";

            }
            finally
            {
                hsvFrame.Dispose();
                thresholdMask.Dispose();
                croppedMat.Dispose();
                mask.Dispose();
            }


        }
    }



}
