using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace LEDVision.Camera
{
    // Một group LED: tên, danh sách ROI, bán kính ROI, ngưỡng diện tích, dải HSV.
    // INotifyPropertyChanged để bảng group ở trang Vision cập nhật theo slider / đổi tên.
    public class GroupLED : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Tên group (người dùng đặt). Step VISION CHECK gọi group theo tên này.
        private string name = "";
        public string Name
        {
            get { return name ?? ""; }
            set { name = value ?? ""; OnPropertyChanged(); }
        }

        public override string ToString()
        {
            return Name;
        }

        private ObservableCollection<SingleLED> colection = new ObservableCollection<SingleLED>();

        public ObservableCollection<SingleLED> Colection
        {
            get
            {
                return colection;
            }
            set
            {
                colection = value;
                OnPropertyChanged();
            }
        }

        private double contourArea;
        public double ContourArea
        {
            get
            {
                return contourArea;
            }
            set
            {
                contourArea = value;
                OnPropertyChanged();
            }
        }

        private int roiRadius = 8;
        public int RoiRadius
        {
            get { return roiRadius; }
            set
            {
                roiRadius = value;
                OnPropertyChanged();
            }
        }

        private HSVColorSpace hsv = new HSVColorSpace();
        public HSVColorSpace HSV
        {
            get { return hsv; }
            set
            {
                hsv = value;
                OnPropertyChanged();
            }
        }

        // Gọi sau khi sửa HSV.HUE / SAT / VAL từ ô Set hoặc histogram → các ô H/S/V trong bảng group cập nhật
        public void NotifyHsvChanged()
        {
            OnPropertyChanged(nameof(HSV));
        }

        private bool maintainState = true;
        public bool MaintainState
        {
            get { return maintainState; }
            set
            {
                foreach (var item in Colection)
                {
                    item.ResultFinal = SingleLED.RESULT.UNKNOWN;
                }
                maintainState = value;
            }
        }
    }
}
