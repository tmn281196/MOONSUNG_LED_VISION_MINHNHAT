using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace LEDVision.Model
{
    // Tên lệnh của một step (chuỗi lưu trong model file, IN HOA)
    public static class StepCmd
    {
        public const string Power = "POWER";          // Sub: ON / OFF
        public const string Relay = "RELAY";          // Sub: ON / OFF, Target: 1..5
        public const string Delay = "DELAY";          // Spec: ms
        public const string Vision = "VISION CHECK";  // Target: tên group, Timeout: thời gian lấy mẫu (ms)

        public static readonly string[] All = { Power, Relay, Delay, Vision };
        public static readonly string[] OnOff = { "ON", "OFF" };

        public const int DefaultVisionMs = 2000;

        public static string Canonical(string cmd)
        {
            string c = (cmd ?? "").Trim().ToUpperInvariant();
            switch (c)
            {
                case "POWER": return Power;
                case "RELAY": return Relay;
                case "DELAY": return Delay;
                case "VISION": case "VISION CHECK": case "CHECK": return Vision;
                default: return c;
            }
        }

        public static bool HasSub(string cmd)
        {
            string c = Canonical(cmd);
            return c == Power || c == Relay;
        }

        public static bool HasTarget(string cmd)
        {
            string c = Canonical(cmd);
            return c == Relay || c == Vision;
        }

        public static bool HasSpec(string cmd)
        {
            return Canonical(cmd) == Delay;
        }

        // Timeout: VISION CHECK = thời gian lấy mẫu; POWER / RELAY = chờ sau khi đóng-mở (trống = không chờ)
        public static bool HasTimeout(string cmd)
        {
            string c = Canonical(cmd);
            return c == Vision || c == Relay || c == Power;
        }
    }

    // Một bước trong chuỗi test (lưu trong model file). Value / Result / Takt chỉ để hiển thị lúc chạy.
    public class TestStep : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private int no;
        public int No
        {
            get { return no; }
            set { if (no != value) { no = value; OnPropertyChanged(); } }
        }

        private bool skip;
        public bool Skip
        {
            get { return skip; }
            set { if (skip != value) { skip = value; OnPropertyChanged(); } }
        }

        private string cmd = StepCmd.Delay;
        public string Cmd
        {
            get { return cmd; }
            set { cmd = StepCmd.Canonical(value); OnPropertyChanged(); OnPropertyChanged(nameof(Label)); }
        }

        private string sub = "";
        public string Sub
        {
            get { return sub; }
            set { sub = (value ?? "").Trim().ToUpperInvariant(); OnPropertyChanged(); OnPropertyChanged(nameof(Label)); }
        }

        // RELAY: số relay 1..5. VISION CHECK: tên group.
        private string target = "";
        public string Target
        {
            get { return target; }
            set { target = (value ?? "").Trim(); OnPropertyChanged(); OnPropertyChanged(nameof(Label)); }
        }

        // DELAY: ms
        private string spec = "";
        public string Spec
        {
            get { return spec; }
            set { spec = (value ?? "").Trim(); OnPropertyChanged(); }
        }

        // VISION CHECK: thời gian lấy mẫu (ms)
        private string timeout = "";
        public string Timeout
        {
            get { return timeout; }
            set { timeout = (value ?? "").Trim(); OnPropertyChanged(); }
        }

        private string comment = "";
        public string Comment
        {
            get { return comment; }
            set { comment = value ?? ""; OnPropertyChanged(); }
        }

        // ---- Chỉ hiển thị lúc chạy, không lưu ----
        private string value = "";
        [JsonIgnore]
        public string Value
        {
            get { return this.value; }
            set { this.value = value ?? ""; OnPropertyChanged(); }
        }

        private string result = "";
        [JsonIgnore]
        public string Result
        {
            get { return result; }
            set { result = value ?? ""; OnPropertyChanged(); }
        }

        private string takt = "";
        [JsonIgnore]
        public string Takt
        {
            get { return takt; }
            set { takt = value ?? ""; OnPropertyChanged(); }
        }

        // Nhãn gộp: "POWER ON", "RELAY 2 OFF", "DELAY 500", "VISION CHECK 7-Segment"
        [JsonIgnore]
        public string Label
        {
            get
            {
                string c = StepCmd.Canonical(cmd);
                if (c == StepCmd.Relay) return c + " " + target + " " + sub;
                if (c == StepCmd.Power) return c + " " + sub;
                if (c == StepCmd.Vision) return c + " " + target;
                return c;
            }
        }

        public int SpecMs(int fallback)
        {
            int ms;
            if (int.TryParse(spec, out ms) && ms >= 0) return ms;
            return fallback;
        }

        public int TimeoutMs(int fallback)
        {
            int ms;
            if (int.TryParse(timeout, out ms) && ms > 0) return ms;
            return fallback;
        }

        public int RelayIndex()
        {
            int n;
            if (int.TryParse(target, out n) && n >= 1 && n <= 5) return n;
            return 0;
        }

        public void ClearResult()
        {
            Value = "";
            Result = "";
            Takt = "";
        }

        public TestStep Duplicate()
        {
            return new TestStep
            {
                Skip = Skip,
                Cmd = Cmd,
                Sub = Sub,
                Target = Target,
                Spec = Spec,
                Timeout = Timeout,
                Comment = Comment,
            };
        }
    }

    public static class TestStepList
    {
        // Đánh lại số thứ tự 1..n
        public static void Renumber(ObservableCollection<TestStep> steps)
        {
            if (steps == null) return;
            for (int i = 0; i < steps.Count; i++) steps[i].No = i + 1;
        }

        public static void ClearResults(IEnumerable<TestStep> steps)
        {
            if (steps == null) return;
            foreach (var s in steps) s.ClearResult();
        }
    }
}
