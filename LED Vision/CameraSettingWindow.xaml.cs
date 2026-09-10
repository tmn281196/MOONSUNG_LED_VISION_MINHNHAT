using System;
using System.ComponentModel;
using System.Windows;
using LEDVision.Camera;

namespace LEDVision
{
    /// <summary>
    /// Cửa sổ hiển thị / chỉnh thông số camera dùng chung cho mọi đối tượng (4-LED, DP, 7-SEG).
    /// Bind trực tiếp vào CameraSetting.Instance.cameraSettingValues nên thay đổi áp dụng ngay lên camera.
    /// </summary>
    public partial class CameraSettingWindow : Window
    {
        private readonly CameraSettingValues values;

        public CameraSettingWindow()
        {
            InitializeComponent();

            values = CameraSetting.Instance.cameraSettingValues;
            DataContext = values;

            // Áp dụng trực tiếp lên camera mỗi khi một thông số thay đổi (kéo slider / nhập số)
            values.PropertyChanged += Values_PropertyChanged;
        }

        private void Values_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (CameraSetting.Instance.IsSyncingFromCamera) return;
            CameraSetting.Instance.SetSingle(e.PropertyName, values);
        }

        // Lấy lại thông số từ file model đã nạp/lưu gần nhất (binding + camera tự cập nhật)
        private void Revert_Click(object sender, RoutedEventArgs e)
        {
            values.CopyFrom(CameraSetting.Instance.loadedCameraSettingValues);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            values.PropertyChanged -= Values_PropertyChanged;
            base.OnClosed(e);
        }
    }
}
