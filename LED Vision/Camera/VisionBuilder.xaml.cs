using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LEDVision.Camera
{
    /// <summary>
    /// Interaction logic for VisionBuilder.xaml
    /// </summary>
    public partial class VisionBuilder : UserControl
    {

        private Model.Model programModel = new Model.Model();

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
                    mainCanvas.Children.Clear();
                    foreach (var led in ProgramModel.Vision.AllLeds())
                    {
                        led.SetParentCanvas(mainCanvas);
                        mainCanvas.Children.Add(led.Roi);
                    }
                }
            }
        }

        private Rectangle rect;
        private Ellipse ellipse;

        private int roiRadius = 20;
        public int RoiRadius
        {
            get { return roiRadius; }
            set
            {
                if (value != null)
                {
                    roiRadius = value;
                }
            }
        }

        private GroupLED selectedVisionObject = null;

        public GroupLED SelectedVisionObject
        {
            set
            {
                selectedVisionObject = value;
                if (selectedVisionObject == null)
                {
                    ProgramModel.Vision.DisableMouseEvent();

                }
                else
                {
                    ProgramModel.Vision.EnableMouseEvent();

                    mainCanvas.Children.Remove(rectangleSelection);

                }

            }
            get
            {
                return selectedVisionObject;
            }

        }




        private Point startPoint;
        public SingleLED selectedLed;


        private List<SingleLED> selectedLeds = new List<SingleLED>();
        private Point multiSelectStartPoint;

        public Rectangle rectangleSelection;

        // Đang có cụm ROI được bôi chọn (khung chọn nét đứt) → phím tắt / xóa / copy tác động lên cả cụm
        private bool HasCluster
        {
            get { return selectedLeds != null && selectedLeds.Count > 0; }
        }

        // Các ROI "đang chọn" cho VisionPage (nét đứt, bảng / đồ thị HSV "Selected"): cụm bôi chọn, không có cụm thì ROI đang chọn lẻ
        public List<SingleLED> SelectedLeds
        {
            get
            {
                if (HasCluster) return new List<SingleLED>(selectedLeds);
                var one = new List<SingleLED>();
                if (selectedLed != null) one.Add(selectedLed);
                return one;
            }
        }

        // Bỏ cụm đang chọn (xóa khung chọn)
        private void ClearCluster()
        {
            if (rectangleSelection != null) mainCanvas.Children.Remove(rectangleSelection);
            rectangleSelection = null;
            selectedLeds = new List<SingleLED>();
            SyncClusterFlags();
        }

        // Đặt cụm đang chọn = danh sách này (vẽ khung bao quanh, đánh dấu InCluster cho từng ROI)
        private void SetCluster(List<SingleLED> leds)
        {
            selectedLeds = leds ?? new List<SingleLED>();
            if (selectedLeds.Count == 0)
            {
                ClearCluster();
                return;
            }
            SetSelectionRectAround(selectedLeds);
            SyncClusterFlags();
        }

        private void SyncClusterFlags()
        {
            if (ProgramModel == null) return;
            foreach (var led in ProgramModel.Vision.AllLeds())
                led.InCluster = selectedLeds != null && selectedLeds.Contains(led);
        }

        public VisionBuilder()
        {
            InitializeComponent();
        }

        // Đặt kích thước bề mặt vẽ ROI = đúng độ phân giải ảnh thô.
        // Khi đó toạ độ ROI chính là toạ độ ảnh thô (UI chỉ scale bằng Viewbox).
        public void SetSurfaceSize(double w, double h)
        {
            if (w <= 0 || h <= 0) return;
            this.Width = w;
            this.Height = h;
            mainGrid.Width = w;
            mainGrid.Height = h;
            mainCanvas.Width = w;
            mainCanvas.Height = h;
        }
        // ====== Chuột trên canvas (một chế độ duy nhất, không còn nút Group Select) ======
        // Double-click TRÁI chỗ trống : thêm ROI tại điểm bấm.
        // Kéo TRÁI trên 1 ROI         : di chuyển ROI đó (handler của chính Ellipse xử lý), bỏ cụm đang chọn.
        // Kéo TRÁI chỗ trống          : bôi khung chọn → các ROI trong khung thành cụm đang chọn.
        // Kéo TRÁI trong khung chọn   : di chuyển cả cụm (bấm lên ROI thuộc cụm cũng kéo cả cụm). Bấm chỗ trống không kéo → bỏ chọn cụm.
        // Ctrl + click TRÁI lên ROI    : thêm / bớt ROI đó vào cụm đang chọn.
        private bool isMovingCluster = false;
        private bool isDrawingSelection = false;

        private static bool IsPointInRect(Rectangle rect, Point p)
        {
            if (rect == null) return false;
            double l = Canvas.GetLeft(rect), t = Canvas.GetTop(rect);
            return p.X >= l && p.X <= l + rect.Width && p.Y >= t && p.Y <= t + rect.Height;
        }

        // Thêm 1 ROI vào nhóm hiện tại tại điểm p
        private SingleLED AddRoiAt(Point p)
        {
            var led = new SingleLED()
            {
                RoiPoint = p,
                RoiRadius = RoiRadius
            };
            led.SetParentCanvas(mainCanvas);
            SelectedVisionObject.Colection.Add(led);
            mainCanvas.Children.Add(led.Roi);
            selectedLed = led;
            return led;
        }

        // Vẽ lại khung chọn bao quanh một cụm ROI (dùng sau paste / duplicate ở chế độ Group Select)
        private void SetSelectionRectAround(List<SingleLED> leds)
        {
            if (leds == null || leds.Count == 0) return;
            double l = leds.Min(x => x.RoiPoint.X - x.RoiRadius) - 4;
            double t = leds.Min(x => x.RoiPoint.Y - x.RoiRadius) - 4;
            double r = leds.Max(x => x.RoiPoint.X + x.RoiRadius) + 4;
            double b = leds.Max(x => x.RoiPoint.Y + x.RoiRadius) + 4;
            if (rectangleSelection != null) mainCanvas.Children.Remove(rectangleSelection);
            rectangleSelection = new Rectangle()
            {
                Stroke = Brushes.White,
                StrokeDashArray = new DoubleCollection() { 2, 2 },
                StrokeThickness = 1,
                Width = r - l,
                Height = b - t,
            };
            Canvas.SetLeft(rectangleSelection, l);
            Canvas.SetTop(rectangleSelection, t);
            mainCanvas.Children.Add(rectangleSelection);
        }

        // Chuột phải trên ảnh → VisionPage đưa ảnh về tỉ lệ 1:1 (reset zoom / pan)
        public event EventHandler ResetZoomRequested;

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // ----- Chuột phải: về tỉ lệ 1:1 (kể cả khi chưa chọn group) -----
            if (e.ChangedButton == MouseButton.Right)
            {
                ResetZoomRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                Keyboard.Focus(mainCanvas);
                return;
            }
            if (SelectedVisionObject == null) return;
            Point p = e.GetPosition(mainCanvas);
            if (e.ChangedButton != MouseButton.Left) return;

            var hit = SelectedVisionObject.Colection.FirstOrDefault(led => led.IsPointInsideEllipse(p, led.Roi));

            // ----- Double-click trái lên chỗ trống: thêm ROI -----
            if (e.ClickCount == 2)
            {
                if (hit == null)
                {
                    ClearCluster();
                    AddRoiAt(p);
                }
                e.Handled = true;
                Keyboard.Focus(mainCanvas);
                return;
            }

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            if (hit != null && ctrl)
            {
                // Ctrl+click lên ROI: thêm / bớt vào cụm
                var list = selectedLeds != null ? new List<SingleLED>(selectedLeds) : new List<SingleLED>();
                if (list.Contains(hit)) list.Remove(hit); else list.Add(hit);
                SetCluster(list);
                selectedLed = hit;
                e.Handled = true;
                Keyboard.Focus(mainCanvas);
                return;
            }

            if (hit != null && !hit.InCluster)
            {
                // Bấm lên 1 ROI ngoài cụm: chọn nó, kéo do Roi_MouseDown/Move/Up của SingleLED xử lý. Cụm đang chọn bị bỏ.
                startPoint = p;
                selectedLed = hit;
                ClearCluster();
                return;
            }

            multiSelectStartPoint = p;
            if (HasCluster && (hit != null || IsPointInRect(rectangleSelection, p)))
            {
                // Bấm trong khung chọn → kéo cả cụm
                isMovingCluster = true;
            }
            else
            {
                // Bấm chỗ trống → bắt đầu bôi khung chọn mới
                ClearCluster();
                selectedLed = null;
                rectangleSelection = new Rectangle()
                {
                    Stroke = Brushes.White,
                    StrokeDashArray = new DoubleCollection() { 2, 2 },
                    StrokeThickness = 1,
                    Width = 0,
                    Height = 0,
                };
                Canvas.SetLeft(rectangleSelection, p.X);
                Canvas.SetTop(rectangleSelection, p.Y);
                mainCanvas.Children.Add(rectangleSelection);
                isDrawingSelection = true;
            }
            mainCanvas.CaptureMouse();
            Keyboard.Focus(mainCanvas);
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (SelectedVisionObject == null) return;
            if (e.LeftButton != MouseButtonState.Pressed || rectangleSelection == null) return;

            Point cur = e.GetPosition(mainCanvas);

            if (isDrawingSelection)
            {
                double offsetX = cur.X - multiSelectStartPoint.X;
                double offsetY = cur.Y - multiSelectStartPoint.Y;
                rectangleSelection.Width = Math.Abs(offsetX);
                rectangleSelection.Height = Math.Abs(offsetY);
                Canvas.SetLeft(rectangleSelection, offsetX < 0 ? cur.X : multiSelectStartPoint.X);
                Canvas.SetTop(rectangleSelection, offsetY < 0 ? cur.Y : multiSelectStartPoint.Y);
            }
            else if (isMovingCluster)
            {
                double offsetX = cur.X - multiSelectStartPoint.X;
                double offsetY = cur.Y - multiSelectStartPoint.Y;
                Canvas.SetLeft(rectangleSelection, Canvas.GetLeft(rectangleSelection) + offsetX);
                Canvas.SetTop(rectangleSelection, Canvas.GetTop(rectangleSelection) + offsetY);
                if (selectedLeds != null)
                {
                    foreach (var led in selectedLeds)
                    {
                        led.RoiPoint = new Point(led.RoiPoint.X + offsetX, led.RoiPoint.Y + offsetY);
                    }
                }
                multiSelectStartPoint = cur;
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (SelectedVisionObject == null) return;
            if (e.ChangedButton != MouseButton.Left) return; // chuột phải đã xử lý ở MouseDown

            if (isDrawingSelection && rectangleSelection != null)
            {
                // Bôi không trúng ROI nào (hoặc chỉ bấm) → không có cụm; có → co khung lại vừa cụm
                SetCluster(SelectedVisionObject.Colection.Where(led => led.IsPointInsideRectangle(rectangleSelection)).ToList());
            }
            isDrawingSelection = false;
            isMovingCluster = false;
            if (mainCanvas.IsMouseCaptured) mainCanvas.ReleaseMouseCapture();
            Keyboard.Focus(mainCanvas);
        }

        // ====== Phím tắt thao tác ROI ======
        // Mũi tên (←↑→↓): di chuyển (1 cái hoặc cả cụm)
        // Ctrl+D: nhân bản | Ctrl+C: copy | Ctrl+V: paste
        // Delete: xóa cái/cụm đang chọn | Ctrl+Delete: xóa cả nhóm hiện tại | Ctrl+Shift+Delete: xóa tất cả
        private List<SingleLED> copyBuffer = new List<SingleLED>();
        private int pasteCount = 0;

        // VisionPage gọi vào đây từ PreviewKeyDown → phím tắt chạy ở mọi chỗ trên trang (trừ khi đang gõ ô nhập)
        public void HandleShortcut(KeyEventArgs e)
        {
            mainCanvas_KeyDown(mainCanvas, e);
        }

        private void mainCanvas_KeyDown(object sender, KeyEventArgs e)
        {
            if (SelectedVisionObject == null)
            {
                return;
            }

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            // Giữ phím Ctrl+V / Ctrl+D / Ctrl+C không được lặp theo auto-repeat
            if (e.IsRepeat && ctrl && (e.Key == Key.C || e.Key == Key.V || e.Key == Key.D))
            {
                e.Handled = true;
                return;
            }

            // ----- Xóa -----
            if (e.Key == Key.Delete && ctrl && shift)
            {
                foreach (var g in ProgramModel.Vision.Groups.ToList()) ClearGroup(g);
                e.Handled = true;
                Keyboard.Focus(mainCanvas);
                return;
            }
            if (e.Key == Key.Delete && ctrl)
            {
                ClearGroup(SelectedVisionObject);
                e.Handled = true;
                Keyboard.Focus(mainCanvas);
                return;
            }

            // ----- Copy / Paste / Duplicate -----
            if (e.Key == Key.C && ctrl)
            {
                CopySelection();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.V && ctrl)
            {
                PasteSelection();
                e.Handled = true;
                Keyboard.Focus(mainCanvas);
                return;
            }
            if (e.Key == Key.D && ctrl)
            {
                DuplicateSelection();
                e.Handled = true;
                Keyboard.Focus(mainCanvas);
                return;
            }

            // ----- Di chuyển + xóa: đang có cụm bôi chọn → cả cụm, không thì ROI đang chọn -----
            if (HasCluster)
            {
                switch (e.Key)
                {
                    case Key.Delete: DeleteCluster(); break;
                    case Key.Left: MoveCluster(-1, 0); break;
                    case Key.Up: MoveCluster(0, -1); break;
                    case Key.Right: MoveCluster(1, 0); break;
                    case Key.Down: MoveCluster(0, 1); break;
                    default: return;
                }
            }
            else
            {
                switch (e.Key)
                {
                    case Key.Delete: DeleteSingle(); break;
                    case Key.Left: MoveSingle(-1, 0); break;
                    case Key.Up: MoveSingle(0, -1); break;
                    case Key.Right: MoveSingle(1, 0); break;
                    case Key.Down: MoveSingle(0, 1); break;
                    default: return;
                }
            }

            e.Handled = true;
            Keyboard.Focus(mainCanvas);
        }

        // Xóa toàn bộ ROI của một nhóm
        private void ClearGroup(GroupLED group)
        {
            if (group == null) return;
            foreach (var led in group.Colection)
            {
                mainCanvas.Children.Remove(led.Roi);
            }
            group.Colection.Clear();
        }

        // Gắn mọi ROI của một group mới (Duplicate group) lên canvas
        public void AttachGroup(GroupLED g)
        {
            if (g == null) return;
            foreach (var led in g.Colection)
            {
                led.SetParentCanvas(mainCanvas);
                if (!mainCanvas.Children.Contains(led.Roi)) mainCanvas.Children.Add(led.Roi);
            }
        }

        // Xóa mọi ROI của group đang chọn (VisionPage gọi trước khi xóa group)
        public void ClearSelectedGroup()
        {
            if (SelectedVisionObject == null) return;
            ClearGroup(SelectedVisionObject);
            selectedLed = null;
            if (selectedLeds != null) selectedLeds.Clear();
        }

        // Xóa 1 ROI đang chọn
        private void DeleteSingle()
        {
            if (selectedLed == null) return;
            foreach (var g in ProgramModel.Vision.Groups) g.Colection.Remove(selectedLed);
            mainCanvas.Children.Remove(selectedLed.Roi);
            selectedLed = null;
        }

        // Xóa cả cụm đang chọn
        private void DeleteCluster()
        {
            if (selectedLeds == null) return;
            foreach (var led in selectedLeds)
            {
                mainCanvas.Children.Remove(led.Roi);
                foreach (var g in ProgramModel.Vision.Groups) g.Colection.Remove(led);
            }
            selectedLeds.Clear();
        }

        // Nhích 1 ROI theo (dx, dy)
        private void MoveSingle(int dx, int dy)
        {
            if (selectedLed == null) return;
            selectedLed.RoiPoint = new Point(selectedLed.RoiPoint.X + dx, selectedLed.RoiPoint.Y + dy);
        }

        // Nhích cả cụm theo (dx, dy)
        private void MoveCluster(int dx, int dy)
        {
            if (rectangleSelection != null)
            {
                Canvas.SetLeft(rectangleSelection, Canvas.GetLeft(rectangleSelection) + dx);
                Canvas.SetTop(rectangleSelection, Canvas.GetTop(rectangleSelection) + dy);
            }
            if (selectedLeds == null) return;
            foreach (var led in selectedLeds)
            {
                led.RoiPoint = new Point(led.RoiPoint.X + dx, led.RoiPoint.Y + dy);
            }
        }

        // Nhân bản tại chỗ (1 cái hoặc cả cụm), lệch 1 chút để dễ thấy
        private void DuplicateSelection()
        {
            if (HasCluster)
            {
                var clones = selectedLeds.Clone();
                foreach (var led in clones)
                {
                    led.RoiPoint = new Point(led.RoiPoint.X + 20, led.RoiPoint.Y + 20);
                    led.SetParentCanvas(mainCanvas);
                    mainCanvas.Children.Add(led.Roi);
                    if (SelectedVisionObject != null) SelectedVisionObject.Colection.Add(led);
                }
                SetCluster(clones);              // cụm vừa nhân bản trở thành cụm đang chọn
            }
            else
            {
                if (selectedLed == null) return;
                var clone = selectedLed.Clone();
                clone.RoiPoint = new Point(selectedLed.RoiPoint.X + selectedLed.RoiRadius * 0.5,
                                           selectedLed.RoiPoint.Y + selectedLed.RoiRadius * 0.5);
                clone.SetParentCanvas(mainCanvas);
                mainCanvas.Children.Add(clone.Roi);
                if (SelectedVisionObject != null) SelectedVisionObject.Colection.Add(clone);
                selectedLed = clone;
            }
        }

        // Copy cái/cụm đang chọn vào bộ nhớ tạm
        private void CopySelection()
        {
            copyBuffer = new List<SingleLED>();
            pasteCount = 0;
            if (HasCluster)
            {
                copyBuffer = selectedLeds.Clone();
            }
            else
            {
                if (selectedLed == null) return;
                copyBuffer.Add(selectedLed.Clone());
            }
        }

        // Paste từ bộ nhớ tạm vào nhóm hiện tại (mỗi lần paste lệch thêm để không chồng nhau)
        private void PasteSelection()
        {
            if (copyBuffer == null || copyBuffer.Count == 0 || SelectedVisionObject == null) return;
            pasteCount++;
            double off = 20 * pasteCount; // mỗi lần dán ra một vị trí mới, không chồng lên bản trước
            var pasted = copyBuffer.Clone();
            foreach (var led in pasted)
            {
                led.RoiPoint = new Point(led.RoiPoint.X + off, led.RoiPoint.Y + off);
                led.SetParentCanvas(mainCanvas);
                mainCanvas.Children.Add(led.Roi);
                SelectedVisionObject.Colection.Add(led);
            }
            // Bản dán trở thành cái đang chọn (không phải bản gốc); dán nhiều cái → thành cụm đang chọn
            selectedLed = pasted.LastOrDefault();
            if (pasted.Count > 1) SetCluster(pasted);
            else ClearCluster();
        }
    }
}
