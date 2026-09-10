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

                        CapturingAndCheckingEvent?.Invoke(null, null);

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

                                device.Power = false;
                                device.CylinderDown = false;
                                device.CylinderUp = true;
                                device.SendControl();
                                await Task.Delay((int)mainWindow.settingModel.SettingVal.DelayUPDOWN);
                                device.CylinderUp = false;
                                device.CylinderDown = true;
                                device.SendControl();

                                await Task.Delay(500);
                                device.Power = true;
                                device.SendControl();
                                //mainWindow.programModel.Vision.SevenSEG.MaintainState = true;
                                //mainWindow.programModel.Vision.FourLED.MaintainState = true;
                                //mainWindow.programModel.Vision.DecimalPoint.MaintainState = true;
                                await Task.Delay((int)mainWindow.settingModel.SettingVal.WaitRetest);

                                device.TriggerTest = false;
                                device.IgnoreTrigger = false;

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