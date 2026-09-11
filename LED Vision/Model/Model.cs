using LEDVision.Camera;
using LEDVision.Properties;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        // ---- Chuỗi test (sequence): chạy tuần tự ở trang Auto sau khi xi lanh xuống ----
        private ObservableCollection<TestStep> testSteps = new ObservableCollection<TestStep>();

        public ObservableCollection<TestStep> TestSteps
        {
            get { return testSteps; }
            set { if (value != null) testSteps = value; }
        }

        // Model chưa có step nào (file cũ / model mới) → dựng chuỗi mặc định:
        // POWER ON → DELAY 1000 → với mỗi group i: RELAY i ON → VISION CHECK group → RELAY i OFF
        // (relay i cấp chung cho nhóm LED i của bảng LED quét; group thứ 6 trở đi không có relay).
        // Trả về true nếu có thêm.
        public bool EnsureDefaultSteps()
        {
            if (testSteps.Count > 0) return false;
            testSteps.Add(new TestStep { Cmd = StepCmd.Power, Sub = "ON", Comment = "LED power on" });
            testSteps.Add(new TestStep { Cmd = StepCmd.Delay, Spec = "1000", Comment = "Wait for LEDs to light up" });
            int relay = 1;
            foreach (var g in vision.Groups)
            {
                bool hasRelay = relay <= 5;
                if (hasRelay) testSteps.Add(new TestStep { Cmd = StepCmd.Relay, Sub = "ON", Target = relay.ToString(), Timeout = "300", Comment = "Select " + g.Name });
                testSteps.Add(new TestStep { Cmd = StepCmd.Vision, Target = g.Name, Timeout = StepCmd.DefaultVisionMs.ToString(), Comment = "Check " + g.Name });
                if (hasRelay) testSteps.Add(new TestStep { Cmd = StepCmd.Relay, Sub = "OFF", Target = relay.ToString(), Comment = "Release " + g.Name });
                relay++;
            }
            testSteps.Add(new TestStep { Cmd = StepCmd.Power, Sub = "OFF", Comment = "LED power off" });
            TestStepList.Renumber(testSteps);
            return true;
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
