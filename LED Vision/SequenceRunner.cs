using LEDVision.Camera;
using LEDVision.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace LEDVision
{
    // Chạy chuỗi step (POWER / RELAY / DELAY / VISION CHECK) của model ở trang Auto.
    // Chạy trên thread nền; step VISION CHECK được đẩy lên UI thread (ROI là Ellipse của WPF).
    // Mỗi step ghi Value / Result / Takt vào chính TestStep để bảng ở trang Auto hiện theo.
    public static class SequenceRunner
    {
        public const string PASS = "PASS";
        public const string FAIL = "FAIL";
        public const string SKIP = "-";
        public const string RUN = "RUN";

        // Trả về true = mọi step (không skip) PASS. Hủy giữa chừng → false, các step còn lại để trống.
        public static bool Run(IList<TestStep> steps, DeviceControl device, Vision vision, Dispatcher ui)
        {
            if (steps == null || steps.Count == 0) return false;
            bool allPass = true;

            foreach (var step in steps)
            {
                if (VisionTest.CancelRequested) return false;

                if (step.Skip)
                {
                    step.Value = "";
                    step.Result = SKIP;
                    step.Takt = "";
                    continue;
                }

                step.Value = "";
                step.Result = RUN;
                step.Takt = "";
                var sw = Stopwatch.StartNew();
                bool ok;
                try
                {
                    ok = RunOne(step, device, vision, ui);
                }
                catch (Exception ex)
                {
                    step.Value = "ERR " + ex.Message;
                    ok = false;
                }
                sw.Stop();
                step.Takt = (sw.ElapsedMilliseconds / 1000.0).ToString("0.00");

                if (VisionTest.CancelRequested)
                {
                    step.Result = "";
                    return false;
                }
                step.Result = ok ? PASS : FAIL;
                if (!ok) allPass = false;
            }
            return allPass;
        }

        private static bool RunOne(TestStep step, DeviceControl device, Vision vision, Dispatcher ui)
        {
            switch (StepCmd.Canonical(step.Cmd))
            {
                case StepCmd.Power:
                {
                    bool on = step.Sub != "OFF";
                    if (device == null || !device.IsConnected)
                    {
                        step.Value = "DEVICE ERR";
                        return false;
                    }
                    device.Power = on;
                    bool sent = device.SendControlStrict();
                    if (!sent)
                    {
                        step.Value = "DEVICE ERR";
                        return false;
                    }
                    step.Value = on ? "ON" : "OFF";
                    // Timeout: chờ sau khi bật / tắt (ms), trống = không chờ
                    int hold = step.TimeoutMs(0);
                    if (hold > 0)
                    {
                        step.Value += " " + hold + "ms";
                        return DelayUnlessCancelled(hold);
                    }
                    return true;
                }

                case StepCmd.Relay:
                {
                    // Target: số relay 1..5; để trống = tất cả. Timeout: giữ / chờ sau khi đóng-mở (ms), trống = không chờ
                    bool all = string.IsNullOrWhiteSpace(step.Target);
                    int idx = step.RelayIndex();
                    if (!all && idx == 0)
                    {
                        step.Value = "BAD TARGET";   // Target phải là 1..5 hoặc trống
                        return false;
                    }
                    bool on = step.Sub != "OFF";
                    if (device == null || !device.IsConnected)
                    {
                        step.Value = "DEVICE ERR";
                        return false;
                    }
                    bool sent = true;
                    if (all)
                    {
                        for (int i = 0; i < 5 && sent; i++) sent = device.SetRelay(i, on);
                    }
                    else
                    {
                        sent = device.SetRelay(idx - 1, on);
                    }
                    if (!sent)
                    {
                        step.Value = "DEVICE ERR";
                        return false;
                    }
                    step.Value = (all ? "ALL " : "RL" + idx + " ") + (on ? "ON" : "OFF");
                    int hold = step.TimeoutMs(0);
                    if (hold > 0)
                    {
                        step.Value += " " + hold + "ms";
                        return DelayUnlessCancelled(hold);
                    }
                    return true;
                }

                case StepCmd.Delay:
                {
                    int ms = step.SpecMs(-1);
                    if (ms < 0)
                    {
                        step.Value = "BAD SPEC";     // Spec phải là số ms
                        return false;
                    }
                    step.Value = ms + "ms";
                    return DelayUnlessCancelled(ms);
                }

                case StepCmd.Vision:
                {
                    if (vision == null)
                    {
                        step.Value = "NO MODEL";
                        return false;
                    }
                    var group = vision.FindGroup(step.Target);
                    if (group == null)
                    {
                        step.Value = "NO GROUP";     // Target không khớp tên group nào trong model
                        return false;
                    }
                    if (group.Colection.Count == 0)
                    {
                        step.Value = "NO ROI";
                        return false;
                    }
                    int ms = step.TimeoutMs(StepCmd.DefaultVisionMs);
                    Vision.GroupCheckResult r = null;
                    if (ui != null && !ui.CheckAccess())
                        ui.Invoke(new Action(() => { r = vision.InspectGroup(group, ms); }));
                    else
                        r = vision.InspectGroup(group, ms);
                    if (VisionTest.CancelRequested) return false;
                    if (r == null || r.Samples == 0)
                    {
                        step.Value = "NO FRAME";     // camera không có khung hình
                        return false;
                    }
                    step.Value = (r.LedCount - r.NgLeds) + "/" + r.LedCount + " OK";
                    return r.Pass;
                }

                default:
                    step.Value = "BAD CMD";
                    return false;
            }
        }

        // Chờ ms, thoát sớm khi hủy. false = bị hủy.
        private static bool DelayUnlessCancelled(int ms)
        {
            int left = ms;
            while (left > 0)
            {
                if (VisionTest.CancelRequested) return false;
                int chunk = Math.Min(50, left);
                Task.Delay(chunk).Wait();
                left -= chunk;
            }
            return !VisionTest.CancelRequested;
        }
    }
}
