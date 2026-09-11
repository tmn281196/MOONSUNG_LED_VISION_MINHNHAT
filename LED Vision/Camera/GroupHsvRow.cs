using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LEDVision.Camera
{
    // Một dòng của bảng group / HSV ở trang Vision: mỗi group 3 dòng H / S / V.
    // Group, ROI chỉ hiện ở dòng H. Min / Max sửa thẳng trong ô (ghi vào HSV của group).
    // Selected / Unselected / All là số đo (min ~ max) do VisionPage đổ vào cho group đang chọn.
    public class GroupHsvRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public GroupLED Group { get; private set; }
        public string Ch { get; private set; }          // "H" / "S" / "V"
        private readonly double maxAllowed;

        public GroupHsvRow(GroupLED group, string ch)
        {
            Group = group;
            Ch = ch;
            maxAllowed = ch == "H" ? 179 : 255;
            group.PropertyChanged += Group_PropertyChanged;
        }

        private void Group_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Tên / số ROI / HSV của group đổi từ chỗ khác (ô Set, histogram, slider, Add ROI) → làm mới ô
            Raise(nameof(GroupName));
            Raise(nameof(RoiCount));
            Raise(nameof(Min));
            Raise(nameof(Max));
        }

        // HSV của group có thể bị thay cả object (Clone) → luôn lấy lại theo kênh
        private ColorComponent Comp
        {
            get
            {
                if (Ch == "H") return Group.HSV.HUE;
                if (Ch == "S") return Group.HSV.SAT;
                return Group.HSV.VAL;
            }
        }

        public bool IsFirst
        {
            get { return Ch == "H"; }
        }

        public string GroupName
        {
            get { return IsFirst ? Group.Name : ""; }
            set
            {
                // Sửa tên ở bất kỳ dòng nào của group; tên rỗng bị bỏ qua (VisionPage kiểm tra trùng sau khi commit)
                if (!string.IsNullOrWhiteSpace(value)) Group.Name = value.Trim();
                Raise();
            }
        }

        public string RoiCount
        {
            get { return IsFirst ? Group.Colection.Count.ToString() : ""; }
        }

        public double Min
        {
            get { return Comp.Min; }
            set
            {
                Comp.Min = Clamp(value);
                if (Comp.Max < Comp.Min) Comp.Max = Comp.Min;
                Raise();
                Raise(nameof(Max));
            }
        }

        public double Max
        {
            get { return Comp.Max; }
            set
            {
                Comp.Max = Clamp(value);
                if (Comp.Min > Comp.Max) Comp.Min = Comp.Max;
                Raise();
                Raise(nameof(Min));
            }
        }

        private double Clamp(double v)
        {
            if (double.IsNaN(v)) return 0;
            v = Math.Round(v);
            if (v < 0) v = 0;
            if (v > maxAllowed) v = maxAllowed;
            return v;
        }

        // ---- Số đo, chỉ có ở group đang chọn ----
        private string selected = "", unselected = "", all = "";

        public string Selected
        {
            get { return selected; }
            set { if (selected != value) { selected = value ?? ""; Raise(); } }
        }

        public string Unselected
        {
            get { return unselected; }
            set { if (unselected != value) { unselected = value ?? ""; Raise(); } }
        }

        public string All
        {
            get { return all; }
            set { if (all != value) { all = value ?? ""; Raise(); } }
        }

        public void RefreshAll()
        {
            Group_PropertyChanged(this, new PropertyChangedEventArgs(""));
        }
    }
}
