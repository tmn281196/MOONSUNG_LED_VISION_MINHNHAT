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
                    foreach (var led in ProgramModel.Vision.DecimalPoint.Colection)
                    {
                        led.SetParentCanvas(mainCanvas);
                        mainCanvas.Children.Add(led.Roi);
                    }
                    foreach (var led in ProgramModel.Vision.SevenSEG.Colection)
                    {
                        led.SetParentCanvas(mainCanvas);
                        mainCanvas.Children.Add(led.Roi);
                    }
                    foreach (var led in ProgramModel.Vision.FourLED.Colection)
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

        private bool isDraggingMultipleLeds = false;

        public bool IsDraggingMultipleLeds
        {
            set
            {
                isDraggingMultipleLeds = value;
                if (value)
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
                return isDraggingMultipleLeds;
            }

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
        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (SelectedVisionObject != null)
            {
                if (IsDraggingMultipleLeds)
                {
                    if (e.LeftButton == MouseButtonState.Pressed)
                    {
                        if (rectangleSelection != null)
                        {
                            mainCanvas.Children.Remove(rectangleSelection);
                        }

                        multiSelectStartPoint = e.GetPosition(mainCanvas);


                        rectangleSelection = new Rectangle()
                        {
                            Stroke = Brushes.White,
                            StrokeDashArray = new DoubleCollection() { 2, 2 }, // Dashed line
                            StrokeThickness = 1,
                        };

                        mainCanvas.Children.Add(rectangleSelection);
                    }

                    if (e.RightButton == MouseButtonState.Pressed)
                    {
                        multiSelectStartPoint = e.GetPosition(mainCanvas);
                    }
                }

                else
                {

                    startPoint = e.GetPosition(mainCanvas);

                    if (SelectedVisionObject != null)
                    {
                        selectedLed = SelectedVisionObject.Colection.FirstOrDefault(singleLed => singleLed.IsPointInsideEllipse(startPoint, singleLed.Roi));



                    }




                    if (selectedLed == null)
                    {
                        Keyboard.ClearFocus();
                        FocusManager.SetFocusedElement(mainCanvas, null);

                        ellipse = new Ellipse()
                        {
                            Stroke = Brushes.Green,
                            StrokeThickness = 1,
                            Width = RoiRadius * 2,
                            Height = RoiRadius * 2,
                        };

                        Canvas.SetLeft(ellipse, startPoint.X - RoiRadius);
                        Canvas.SetTop(ellipse, startPoint.Y - RoiRadius);
                        mainCanvas.Children.Add(ellipse);
                    }
                }
            }

        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (SelectedVisionObject != null)
            {
                if (IsDraggingMultipleLeds && rectangleSelection != null)
                {
                    if (e.LeftButton == MouseButtonState.Pressed)
                    {

                        Point currentPoint = e.GetPosition(mainCanvas);


                        double offsetX = currentPoint.X - multiSelectStartPoint.X;
                        double offsetY = currentPoint.Y - multiSelectStartPoint.Y;

                        rectangleSelection.Height = Math.Abs(offsetY);
                        rectangleSelection.Width = Math.Abs(offsetX);

                        double newY = (offsetY < 0) ? currentPoint.Y : multiSelectStartPoint.Y;
                        double newX = (offsetX < 0) ? currentPoint.X : multiSelectStartPoint.X;

                        Canvas.SetLeft(rectangleSelection, newX);
                        Canvas.SetTop(rectangleSelection, newY);
                    }
                    if (e.RightButton == MouseButtonState.Pressed)
                    {

                        Point currentPoint = e.GetPosition(mainCanvas);

                        double offsetX = currentPoint.X - multiSelectStartPoint.X;
                        double offsetY = currentPoint.Y - multiSelectStartPoint.Y;

                        Canvas.SetLeft(rectangleSelection, Canvas.GetLeft(rectangleSelection) + offsetX);
                        Canvas.SetTop(rectangleSelection, Canvas.GetTop(rectangleSelection) + offsetY);

                        if (selectedLeds.Count > 0 || selectedLeds != null)
                        {
                            selectedLeds.ForEach(led =>
                            {
                                led.RoiPoint = new Point(led.RoiPoint.X + offsetX, led.RoiPoint.Y + offsetY);
                            });
                        }

                        multiSelectStartPoint = currentPoint;
                    }
                }
                else
                {
                    if (e.LeftButton == MouseButtonState.Pressed && ellipse != null)
                    {
                        try
                        {
                            Point currentPoint = e.GetPosition(mainCanvas);

                            if (SelectedVisionObject != null)
                            {
                                selectedLed = SelectedVisionObject.Colection.FirstOrDefault(led => led.IsPointInsideEllipse(startPoint, led.Roi));
                            }



                            if (selectedLed != null)
                            {
                                Canvas.SetLeft(ellipse, Math.Min(currentPoint.X - selectedLed.RoiRadius, startPoint.X - selectedLed.RoiRadius));
                                Canvas.SetTop(ellipse, Math.Min(currentPoint.Y - selectedLed.RoiRadius, startPoint.Y - selectedLed.RoiRadius));

                            }

                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }

        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (SelectedVisionObject != null)
            {
                if (IsDraggingMultipleLeds && rectangleSelection != null)
                {


                    selectedLeds = SelectedVisionObject.Colection.Where(led => led.IsPointInsideRectangle(rectangleSelection)).ToList();

                }
                else
                {
                    if (ellipse != null)
                    {
                        Point bufPoint = new Point()
                        {
                            X = Canvas.GetLeft(ellipse) + RoiRadius,
                            Y = Canvas.GetTop(ellipse) + RoiRadius,
                        };

                        var singleLed = new SingleLED()
                        {
                            RoiPoint = bufPoint,
                            RoiRadius = RoiRadius
                        };

                        singleLed.SetParentCanvas(mainCanvas);

                        if (SelectedVisionObject != null)
                        {
                            SelectedVisionObject.Colection.Add(singleLed);
                        }

                        mainCanvas.Children.Add(singleLed.Roi);
                        mainCanvas.Children.Remove(ellipse);
                        ellipse = null;
                    }

                }
                Keyboard.Focus(mainCanvas);
            }
        }

        // ====== Phím tắt thao tác ROI ======
        // Mũi tên (←↑→↓): di chuyển (1 cái hoặc cả cụm)
        // Ctrl+D: nhân bản | Ctrl+C: copy | Ctrl+V: paste
        // Delete: xóa cái/cụm đang chọn | Ctrl+Delete: xóa cả nhóm hiện tại | Ctrl+Shift+Delete: xóa tất cả
        private List<SingleLED> copyBuffer = new List<SingleLED>();
        private int pasteCount = 0;

        private void mainCanvas_KeyDown(object sender, KeyEventArgs e)
        {
            if (SelectedVisionObject == null)
            {
                return;
            }

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            // ----- Xóa -----
            if (e.Key == Key.Delete && ctrl && shift)
            {
                ClearGroup(ProgramModel.Vision.FourLED);
                ClearGroup(ProgramModel.Vision.SevenSEG);
                ClearGroup(ProgramModel.Vision.DecimalPoint);
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

            // ----- Di chuyển + xóa cái/cụm đang chọn (theo chế độ hiện tại) -----
            if (IsDraggingMultipleLeds)
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

        // Xóa 1 ROI đang chọn
        private void DeleteSingle()
        {
            if (selectedLed == null) return;
            ProgramModel.Vision.FourLED.Colection.Remove(selectedLed);
            ProgramModel.Vision.SevenSEG.Colection.Remove(selectedLed);
            ProgramModel.Vision.DecimalPoint.Colection.Remove(selectedLed);
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
                ProgramModel.Vision.FourLED.Colection.Remove(led);
                ProgramModel.Vision.SevenSEG.Colection.Remove(led);
                ProgramModel.Vision.DecimalPoint.Colection.Remove(led);
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
            if (IsDraggingMultipleLeds)
            {
                if (selectedLeds == null || selectedLeds.Count == 0) return;
                var clones = selectedLeds.Clone();
                foreach (var led in clones)
                {
                    led.RoiPoint = new Point(led.RoiPoint.X + 20, led.RoiPoint.Y + 20);
                    led.SetParentCanvas(mainCanvas);
                    mainCanvas.Children.Add(led.Roi);
                    if (SelectedVisionObject != null) SelectedVisionObject.Colection.Add(led);
                }
                selectedLeds = clones;
                if (rectangleSelection != null)
                {
                    Canvas.SetLeft(rectangleSelection, Canvas.GetLeft(rectangleSelection) + 20);
                    Canvas.SetTop(rectangleSelection, Canvas.GetTop(rectangleSelection) + 20);
                }
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
            if (IsDraggingMultipleLeds)
            {
                if (selectedLeds == null) return;
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
            double off = 20 * pasteCount;
            var pasted = copyBuffer.Clone();
            foreach (var led in pasted)
            {
                led.RoiPoint = new Point(led.RoiPoint.X + off, led.RoiPoint.Y + off);
                led.SetParentCanvas(mainCanvas);
                mainCanvas.Children.Add(led.Roi);
                SelectedVisionObject.Colection.Add(led);
            }
            selectedLed = pasted.LastOrDefault();
        }
    }
}
