using OpenCvSharp.XPhoto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LEDVision.Camera
{
    public class ColorComponent
    { 
        public ColorComponent(double min, double max) {

            this.Max = max;
            this.Min = min;
        }

        public double Max { get; set; }
        public double Min { get; set; }
        
    }
}
