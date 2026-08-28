using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LEDVision.Camera
{    public class HSVColorSpace
    {
        public ColorComponent HUE { get; set; } = new ColorComponent(0, 179);
        public ColorComponent SAT { get; set; } = new ColorComponent(0, 255);
        public ColorComponent VAL { get; set; } = new ColorComponent(0, 255);
    }
}
