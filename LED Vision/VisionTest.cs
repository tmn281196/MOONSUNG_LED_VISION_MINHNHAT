using DirectShowLib;
using LEDVision.Camera;
using LEDVision.Model;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;

namespace LEDVision
{
    public class VisionTest
    {
        private DeviceControl device;

        public DeviceControl Device
        {
            get
            {
                return device;
            }

            set
            {
                device = value;
            }
        }

        public enum TestState
        {
            Ready,
            Testing
        }

        private TestState currentTestState;

        public TestState CurrentTestState
        {
            get
            {
                return currentTestState;
            }
            set
            {
                if (currentTestState != value)
                {
                    currentTestState = value;
                }
            }
        }

        public string currentPage = "";
        public bool startManual = false;


        public int passCount = 0;
        public int FailCount = 0;


        public int retest = 0;




        public bool PostTestNG = false;
        private byte[] resetTX = { 0x11, 0x22, 0x99, 0x11, 0x22 };



        public event EventHandler TestFinishedEvent;

        // Test bị hủy (xi lanh rời vị trí dưới giữa chừng): không tính PASS / FAIL
        public event EventHandler TestCancelledEvent;

        // Cờ hủy: MainWindow đặt khi nhận cạnh reed "rời vị trí dưới" lúc đang test; InspectAll thấy là thoát sớm
        public static volatile bool CancelRequested = false;

        public event EventHandler TestStartedEvent;

        public event EventHandler CapturingAndCheckingEvent;

        private MainWindow mainWindow;

        public VisionTest(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
        }
        public async void START()

        {
            CurrentTestState = TestState.Ready;
            await Task.Run(TestStart);
        }

        // Chờ ms nhưng thoát sớm khi có yêu cầu hủy. Trả về false nếu bị hủy.
        private static async Task<bool> DelayUnlessCancelled(int ms)
        {
            int left = ms;
            while (left > 0)
            {
                if (CancelRequested) return false;
                int step = Math.Min(50, left);
                await Task.Delay(step);
                left -= step;
            }
            return !CancelRequested;
        }

        private async Task TestStart()
        {
            while (true)
            {
                switch (CurrentTestState)
                {
                    case TestState.Ready:

                        if (Device.TriggerTest && currentPage == "Auto" || startManual && currentPage == "Auto")
                        {
                            startManual = false;
                            retest = 0;
                            TestStartedEvent?.Invoke(null, null);

                        }
                        break;

                    case TestState.Testing:

                        if (!CancelRequested)
                        {
                            CapturingAndCheckingEvent?.Invoke(null, null);
                        }

                        if (CancelRequested)
                        {
                            CancelRequested = false;
                            PostTestNG = false;
                            FailCount = 0;
                            TestCancelledEvent?.Invoke(null, null);
                            break;
                        }

                        PostTestNG = FailCount > 0;


                        if (PostTestNG && mainWindow.settingModel.SettingVal.RetestTimes > 0)
                        {

                            retest++;

                            if (retest <= mainWindow.settingModel.SettingVal.RetestTimes)
                            {                       


                                PostTestNG = false;
                                FailCount = 0;

                                // Retry: app tự điều khiển xi lanh → chặn reed switch suốt chu kỳ này
                                device.IgnoreTrigger = true;
                                device.TriggerTest = false;

                                // Mỗi bước đều kiểm tra cờ hủy (nút CANCEL): hủy là dừng ngay tại chỗ
                                bool cancelled = false;

                                device.Power = false;
                                device.CylinderDown = false;
                                device.CylinderUp = true;
                                device.SendControl();
                                // Lên: chờ cảm biến DOWN nhả = đã rời vị trí dưới, rồi chờ thêm
                                // Delay Between UP/DOWN cho phần hành trình còn lại (không có cảm biến trên)
                                device.WaitForLeaveDown(mainWindow.settingModel.SettingVal.SensorTimeoutMs);
                                cancelled = CancelRequested || !await DelayUnlessCancelled((int)mainWindow.settingModel.SettingVal.DelayUPDOWN);

                                if (!cancelled)
                                {
                                    device.CylinderUp = false;
                                    device.CylinderDown = true;
                                    device.SendControl();
                                    device.WaitForDown(mainWindow.settingModel.SettingVal.SensorTimeoutMs);
                                    cancelled = CancelRequested || !await DelayUnlessCancelled(mainWindow.settingModel.SettingVal.DelayBeforePowerMs);   // "Before power ON"
                                }
                                if (!cancelled)
                                {
                                    device.Power = true;
                                    device.SendControl();
                                    cancelled = !await DelayUnlessCancelled((int)mainWindow.settingModel.SettingVal.WaitRetest);
                                }

                                device.TriggerTest = false;
                                device.IgnoreTrigger = false;

                                if (cancelled)
                                {
                                    // Dừng xi lanh tại chỗ, tắt nguồn, báo hủy (AutoPage hiện READY, không tính kết quả)
                                    CancelRequested = false;
                                    device.Power = false;
                                    device.CylinderUp = false;
                                    device.CylinderDown = false;
                                    device.SendControl();
                                    PostTestNG = false;
                                    FailCount = 0;
                                    TestCancelledEvent?.Invoke(null, null);
                                }

                                break;


                            }

                        }


                        TestFinishedEvent?.Invoke(null, null);

                        break;

                    default:
                        break;
                }
            }
        }
    }
}