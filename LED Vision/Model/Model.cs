using LEDVision.Camera;
using LEDVision.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LEDVision.Model
{
    public class Model
    {

        private Vision vision = new Vision();

        public Vision Vision
        {
            get
            {
                return vision;
            }
            set
            {
                if (vision != null)
                {
                    vision = value;
                }
            }
        }

       

        private CameraSettingValues cameraSettingValues = new CameraSettingValues();

        // Thông số camera dùng chung cho tất cả đối tượng (4-LED, DP, 7-SEG)
        public CameraSettingValues CameraSettingValues
        {
            get
            {
                return cameraSettingValues;
            }
            set
            {
                if (value != null)
                {
                    cameraSettingValues = value;
                }
            }
        }

        // Tương thích ngược: các file model cũ lưu key "SegmentCameraSettingValues".
        // Property chỉ có setter nên không bị ghi ra khi serialize, chỉ dùng khi load file cũ.
        public CameraSettingValues SegmentCameraSettingValues
        {
            set
            {
                if (value != null)
                {
                    cameraSettingValues = value;
                }
            }
        }

    }

    public class SettingModel
    {
        private Setting settingVal = new Setting();

        public Setting SettingVal
        {
            get
            {
                return settingVal;
            }

            set
            {
                if (settingVal != null)
                {
                    settingVal = value;
                }
            }
        }

    }
}