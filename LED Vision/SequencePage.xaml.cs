using LEDVision.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace LEDVision
{
    /// <summary>
    /// Trang soạn chuỗi test (sequence) của model: bảng step bên trái, thẻ soạn bên phải.
    /// Lệnh: POWER (ON/OFF), RELAY (ON/OFF, Target 1..5 hoặc trống = tất cả), DELAY (Spec ms),
    /// VISION CHECK (Target = tên group, Timeout = thời gian lấy mẫu ms).
    /// Danh sách step nằm trong Model.TestSteps → lưu cùng file model (Save ở thanh top).
    /// </summary>
    public partial class SequencePage : Page
    {
        private readonly MainWindow mainWindow;
        private Model.Model programModel;

        public Model.Model ProgramModel
        {
            get { return programModel; }
            set
            {
                programModel = value;
                if (programModel != null)
                {
                    TestStepList.Renumber(programModel.TestSteps);
                    stepsGrid.ItemsSource = programModel.TestSteps;
                }
                else
                {
                    stepsGrid.ItemsSource = null;
                }
                RefreshGroupNames();
            }
        }

        private ObservableCollection<TestStep> Steps
        {
            get { return programModel != null ? programModel.TestSteps : null; }
        }

        public SequencePage(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            InitializeComponent();

            cmdCombo.ItemsSource = StepCmd.All;
            subCombo.ItemsSource = StepCmd.OnOff;
            cmdCombo.SelectedIndex = 0;
            subCombo.SelectedIndex = 0;
            ApplyCmdLayout();
        }

        // ====== Thẻ soạn: bật / tắt ô theo lệnh ======
        private string CurrentCmd
        {
            get { return StepCmd.Canonical(cmdCombo.SelectedItem as string); }
        }

        private void CmdCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyCmdLayout();
        }

        private void ApplyCmdLayout()
        {
            string c = CurrentCmd;
            subCombo.IsEnabled = StepCmd.HasSub(c);
            targetCombo.IsEnabled = StepCmd.HasTarget(c);
            specBox.IsEnabled = StepCmd.HasSpec(c);
            timeoutBox.IsEnabled = StepCmd.HasTimeout(c);

            if (!subCombo.IsEnabled) subCombo.SelectedIndex = -1;
            else if (subCombo.SelectedIndex < 0) subCombo.SelectedIndex = 0;

            RefreshGroupNames();

            switch (c)
            {
                case StepCmd.Power:
                    targetHint.Text = "";
                    specHint.Text = "";
                    timeoutHint.Text = "Optional: wait this long (ms) after switching before the next step.";
                    break;
                case StepCmd.Relay:
                    targetHint.Text = "Relay number 1 to 5. Leave empty = all five relays.";
                    specHint.Text = "";
                    timeoutHint.Text = "Optional: wait this long (ms) after switching before the next step.";
                    break;
                case StepCmd.Delay:
                    targetHint.Text = "";
                    specHint.Text = "Wait time in ms, e.g. 1000.";
                    timeoutHint.Text = "";
                    if (specBox.Text.Trim().Length == 0) specBox.Text = "1000";
                    break;
                case StepCmd.Vision:
                    targetHint.Text = "Name of the LED group to check (Vision page → LED Group).";
                    specHint.Text = "";
                    timeoutHint.Text = "Sampling time in ms (camera checks every 100 ms). Default " + StepCmd.DefaultVisionMs + ".";
                    if (timeoutBox.Text.Trim().Length == 0) timeoutBox.Text = StepCmd.DefaultVisionMs.ToString();
                    break;
                default:
                    targetHint.Text = specHint.Text = timeoutHint.Text = "";
                    break;
            }
        }

        // Danh sách Target: RELAY → 1..5, VISION CHECK → tên group của model đang chỉnh (VisionPage)
        public void RefreshGroupNames()
        {
            if (targetCombo == null) return;
            string keep = targetCombo.Text;
            var items = new List<string>();
            string c = CurrentCmd;
            if (c == StepCmd.Relay)
            {
                items.AddRange(new[] { "1", "2", "3", "4", "5" });
            }
            else if (c == StepCmd.Vision)
            {
                var m = mainWindow != null && mainWindow.visionPage != null ? mainWindow.visionPage.ProgramModel : programModel;
                if (m != null) items.AddRange(m.Vision.Groups.Select(g => g.Name));
            }
            targetCombo.ItemsSource = items;
            targetCombo.Text = keep;
        }

        // ====== Bảng ======
        private bool fillingEditor = false;

        private TestStep Selected
        {
            get { return stepsGrid.SelectedItem as TestStep; }
        }

        private void StepsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var st = Selected;
            if (st == null) return;
            fillingEditor = true;
            try
            {
                cmdCombo.SelectedItem = StepCmd.All.FirstOrDefault(x => x == StepCmd.Canonical(st.Cmd));
                ApplyCmdLayout();
                if (StepCmd.HasSub(st.Cmd)) subCombo.SelectedItem = st.Sub == "OFF" ? "OFF" : "ON";
                targetCombo.Text = st.Target;
                specBox.Text = st.Spec;
                timeoutBox.Text = st.Timeout;
                commentBox.Text = st.Comment;
            }
            finally
            {
                fillingEditor = false;
            }
        }

        // Sửa thẳng trong ô: chuẩn hóa tên lệnh sau khi commit
        private void StepsGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var st = e.Row.Item as TestStep;
                if (st != null) st.Cmd = StepCmd.Canonical(st.Cmd);
            }));
        }

        private TestStep ReadEditor()
        {
            var st = new TestStep
            {
                Cmd = CurrentCmd,
                Sub = subCombo.IsEnabled ? (subCombo.SelectedItem as string ?? "ON") : "",
                Target = targetCombo.IsEnabled ? targetCombo.Text : "",
                Spec = specBox.IsEnabled ? specBox.Text : "",
                Timeout = timeoutBox.IsEnabled ? timeoutBox.Text : "",
                Comment = commentBox.Text,
            };
            return st;
        }

        private bool ValidateEditor(TestStep st)
        {
            string msg = null;
            int n;
            switch (StepCmd.Canonical(st.Cmd))
            {
                case StepCmd.Delay:
                    if (!int.TryParse(st.Spec, out n) || n < 0) msg = "Spec must be a wait time in ms (e.g. 1000).";
                    break;
                case StepCmd.Relay:
                    if (st.Target.Length > 0 && (!int.TryParse(st.Target, out n) || n < 1 || n > 5)) msg = "Target must be a relay number 1 to 5, or empty for all.";
                    break;
                case StepCmd.Vision:
                    if (st.Target.Length == 0) msg = "Target must be the name of an LED group.";
                    else if (!int.TryParse(st.Timeout, out n) || n <= 0) msg = "Timeout must be the sampling time in ms (e.g. 2000).";
                    break;
            }
            if (StepCmd.HasTimeout(st.Cmd) && st.Timeout.Length > 0 && msg == null && (!int.TryParse(st.Timeout, out n) || n < 0))
                msg = "Timeout must be a number of ms.";
            if (msg != null)
            {
                MessageBox.Show(msg, "Test Sequence", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private void Commit(TestStep select)
        {
            TestStepList.Renumber(Steps);
            stepsGrid.Items.Refresh();
            if (select != null)
            {
                stepsGrid.SelectedItem = select;
                stepsGrid.ScrollIntoView(select);
            }
        }

        private void AddStep_Click(object sender, RoutedEventArgs e)
        {
            if (Steps == null) return;
            var st = ReadEditor();
            if (!ValidateEditor(st)) return;
            int idx = Selected != null ? Steps.IndexOf(Selected) + 1 : Steps.Count;
            Steps.Insert(idx, st);
            Commit(st);
        }

        private void UpdateStep_Click(object sender, RoutedEventArgs e)
        {
            var st = Selected;
            if (st == null) return;
            var n = ReadEditor();
            if (!ValidateEditor(n)) return;
            st.Cmd = n.Cmd;
            st.Sub = n.Sub;
            st.Target = n.Target;
            st.Spec = n.Spec;
            st.Timeout = n.Timeout;
            st.Comment = n.Comment;
            Commit(st);
        }

        private void Move(int delta)
        {
            var st = Selected;
            if (st == null || Steps == null) return;
            int i = Steps.IndexOf(st);
            int j = i + delta;
            if (j < 0 || j >= Steps.Count) return;
            Steps.Move(i, j);
            Commit(st);
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e) { Move(-1); }
        private void MoveDown_Click(object sender, RoutedEventArgs e) { Move(1); }

        private void MoveFirst_Click(object sender, RoutedEventArgs e)
        {
            var st = Selected;
            if (st == null) return;
            Steps.Move(Steps.IndexOf(st), 0);
            Commit(st);
        }

        private void MoveLast_Click(object sender, RoutedEventArgs e)
        {
            var st = Selected;
            if (st == null) return;
            Steps.Move(Steps.IndexOf(st), Steps.Count - 1);
            Commit(st);
        }

        private void Duplicate_Click(object sender, RoutedEventArgs e)
        {
            var st = Selected;
            if (st == null) return;
            var d = st.Duplicate();
            Steps.Insert(Steps.IndexOf(st) + 1, d);
            Commit(d);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var st = Selected;
            if (st == null) return;
            int i = Steps.IndexOf(st);
            Steps.Remove(st);
            Commit(Steps.Count > 0 ? Steps[Math.Min(i, Steps.Count - 1)] : null);
        }

        private void DeleteAll_Click(object sender, RoutedEventArgs e)
        {
            if (Steps == null || Steps.Count == 0) return;
            if (MessageBox.Show("Delete all steps?", "Test Sequence", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            Steps.Clear();
            Commit(null);
        }

        private void Default_Click(object sender, RoutedEventArgs e)
        {
            if (programModel == null) return;
            if (Steps.Count > 0 &&
                MessageBox.Show("Replace the current steps with the default sequence?", "Test Sequence", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            Steps.Clear();
            programModel.EnsureDefaultSteps();
            Commit(Steps.Count > 0 ? Steps[0] : null);
        }

        private void SequenceHelp_Click(object sender, RoutedEventArgs e)
        {
            var win = new RoiHelpWindow("sequence");
            try { win.Owner = Window.GetWindow(this); } catch (Exception) { }
            win.Show();
        }
    }
}
