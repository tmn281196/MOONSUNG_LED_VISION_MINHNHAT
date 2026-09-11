using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LEDVision.Camera
{
    public class GroupLED
    {
        // Tên group (người dùng đặt). Step VISION CHECK gọi group theo tên này.
        private string name = "";
        public string Name
        {
            get { return name ?? ""; }
            set { name = value ?? ""; }
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
            }
        }

        private int roiRadius = 8;
        public int RoiRadius
        {
            get { return roiRadius; }
            set
            {
                roiRadius = value;
            }
        }

        private HSVColorSpace hsv = new HSVColorSpace();
        public HSVColorSpace HSV
        {
            get { return hsv; }
            set
            {
                hsv = value;
            }
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
