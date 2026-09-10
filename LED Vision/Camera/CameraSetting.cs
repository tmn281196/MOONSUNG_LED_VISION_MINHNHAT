using DirectShowLib;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using OpenCvSharp.XPhoto;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace LEDVision.Camera

{
    public class CameraSetting : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool _isCameraRunning = true;
        private bool _isUpdatingFrames = true;
        private Task _previewTask;
        private CancellationTokenSource _cancellationTokenSource;
        public VideoCapture _videoCapture;


        public CameraSettingValues cameraSettingValues = new CameraSettingValues();

        // Snapshot thông số của file model đã nạp/lưu gần nhất, dùng cho chức năng Revert
        public CameraSettingValues loadedCameraSettingValues = new CameraSettingValues();


        private static CameraSetting _instance;
        public static CameraSetting Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new CameraSetting();
                }
                return _instance;
            }
        }

        private Mat _lastMatFrame;

        public Mat LastMatFrame
        {
            get { return _lastMatFrame; }
            set
            {
                if (value != null)
                {
                    _lastMatFrame = value;
                }
            }
        }

        private BitmapSource _lastFrame;

        public BitmapSource LastFrame
        {
            get
            {
                return _lastFrame;
            }

            set
            {
                _lastFrame = value;
                NotifyPropertyChanged(nameof(LastFrame));
            }
        }

        public void PauseFrameUpdates()
        {
            _isUpdatingFrames = false; // Stop updating frames
        }

        // Resume frame updates (called when switching to a page that needs the feed)
        public void ResumeFrameUpdates()
        {
            _isUpdatingFrames = true; // Resume updating frames
        }

        public void StopCamera()
        {
            _isCameraRunning = false;
            _ = StopCameraAsync();
        }

        // Dừng hẳn camera: hủy vòng lặp, Dispose VideoCapture, đóng helper DirectShow
        public async Task StopCameraAsync()
        {
            _cameraReady = false;
            try
            {
                _cancellationTokenSource?.Cancel();
            }
            catch (Exception)
            {
            }

            if (_previewTask != null)
            {
                try
                {
                    await _previewTask; // vòng lặp thoát trong ~250 ms và tự Dispose VideoCapture
                }
                catch (Exception)
                {
                }
            }

            try
            {
                _videoCapture?.Dispose();
            }
            catch (Exception)
            {
            }
            _videoCapture = null;
            Control.Close();
            _previewTask = null;
            LastFrameTime = DateTime.MinValue;
        }

        public CameraSetting()
        {
        }

        public void START()
        {
            try
            {
                _isCameraRunning = false;

                _ = StartCamera();
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR starting camera");
            }
        }

        // Camera đang được mở (VideoCapture + DirectShow helper đã sẵn sàng)
        public event EventHandler CameraOpened;

        // Thời điểm nhận khung hình hợp lệ gần nhất (để biết camera còn sống hay đã rớt)
        public DateTime LastFrameTime { get; private set; } = DateTime.MinValue;

        public bool IsCameraOpen
        {
            get
            {
                try { return _videoCapture != null && _videoCapture.IsOpened(); }
                catch (Exception) { return false; }
            }
        }

        // Truy cập trực tiếp thuộc tính camera qua DirectShow (đọc dải giá trị thật, set Pan/Tilt ổn định)
        public CameraControlHelper Control = new CameraControlHelper();

        // Đang đồng bộ giá trị TỪ camera vào cameraSettingValues → UI không được đẩy ngược lại camera
        public bool IsSyncingFromCamera { get; private set; }

        private int _cameraIndex = 0;

        // true sau khi VideoCapture + helper mở xong (OnCameraOpened). Trước đó KHÔNG được đụng _videoCapture
        // từ UI thread (luồng camera đang khởi tạo nó → treo). Thông số sẽ được OnCameraOpened áp sau.
        private volatile bool _cameraReady = false;

        public async Task StartCamera()
        {
            if (_previewTask != null && !_previewTask.IsCompleted) return;  // Prevent reinitializing the task if already running

            _cancellationTokenSource = new CancellationTokenSource();
            _previewTask = Task.Run(async () =>
            {
                try
                {
                    _cameraReady = false;
                    _cameraIndex = CameraControlHelper.FindCameraIndex();
                    _videoCapture = new VideoCapture(_cameraIndex, VideoCaptureAPIs.DSHOW);
                    try
                    {
                        _videoCapture.BufferSize = 1;
                    }
                    catch (Exception)
                    {
                    }

                    // Mở helper + áp thông số hiện tại (mặc định hoặc của model đã nạp) lên camera.
                    // Làm trên UI thread để mọi truy cập COM sau này (slider, lưu model) cùng một apartment.
                    try
                    {
                        var dispatcher = Application.Current?.Dispatcher;
                        if (dispatcher != null)
                        {
                            await dispatcher.InvokeAsync(new Action(OnCameraOpened));
                        }
                        else
                        {
                            OnCameraOpened();
                        }
                    }
                    catch (Exception)
                    {
                    }

                    while (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        using (Mat frame = _videoCapture.RetrieveMat())
                        {
                            if (frame != null && !frame.Empty())
                            {

                                Cv2.Rotate(frame, frame, RotateFlags.Rotate90Counterclockwise);
                                //Cv2.Invert(frame, frame);

                                LastMatFrame = frame.Clone();
                                LastFrameTime = DateTime.Now;
                                var bi = frame.ToBitmapSource();
                                bi.Freeze();
                                LastFrame = bi;
                            }
                            _videoCapture.Grab();
                            await Task.Delay(30);
                        }
                        await Task.Delay(200); // millisecond
                    }

                    _videoCapture?.Dispose();
                    Control.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to capture camera frame");
                }
            }, _cancellationTokenSource.Token);

            if (_previewTask.IsFaulted)
            {
                // To let the exceptions exit
                await _previewTask;
            }
        }

        // Đóng hẳn camera hiện tại rồi mở lại (nút Reconnect Camera ở thanh bottom).
        public async Task RestartCamera()
        {
            await StopCameraAsync();
            await StartCamera();
        }

        private void OnCameraOpened()
        {
            try
            {
                Control.Open(_cameraIndex);
            }
            catch (Exception)
            {
            }
            _cameraReady = true;
            // Camera vừa mở: đưa nó về đúng bộ thông số app đang giữ (mặc định / model đã nạp)
            SetParammeter(cameraSettingValues);
            CameraOpened?.Invoke(this, EventArgs.Empty);
        }

        // Mở hộp thoại thuộc tính của driver (Logitech "Properties": Video Proc Amp / Camera Control).
        // Hàm chặn (modal) cho tới khi đóng hộp thoại; sau đó đọc lại toàn bộ giá trị từ camera.
        public bool ShowDriverDialog()
        {
            if (!_cameraReady || _videoCapture == null) return false;
            try
            {
                _videoCapture.Set(VideoCaptureProperties.Settings, 1);
            }
            catch (Exception)
            {
                return false;
            }
            ReadFromCamera();
            return true;
        }

        // Đọc thông số ĐANG DÙNG THẬT của camera vào cameraSettingValues (kể cả khi người dùng chỉnh
        // ở hộp thoại driver / phần mềm Logitech). Gọi trước khi lưu model để file luôn khớp với camera.
        public bool ReadFromCamera()
        {
            if (!Control.IsOpen)
            {
                return ReadCameraSettingValues();
            }

            IsSyncingFromCamera = true;
            try
            {
                int v;
                CameraControlFlags cf;
                VideoProcAmpFlags pf;

                if (Control.GetCameraControl(CameraControlProperty.Exposure, out v, out cf))
                {
                    cameraSettingValues.Exposure = v;
                    cameraSettingValues.AutoExposure = (cf & CameraControlFlags.Auto) == CameraControlFlags.Auto;
                }
                if (Control.GetCameraControl(CameraControlProperty.Focus, out v, out cf))
                {
                    cameraSettingValues.Focus = v;
                    cameraSettingValues.AutoFocus = (cf & CameraControlFlags.Auto) == CameraControlFlags.Auto;
                }
                if (Control.GetCameraControl(CameraControlProperty.Zoom, out v, out cf)) cameraSettingValues.Zoom = v;
                if (Control.GetCameraControl(CameraControlProperty.Pan, out v, out cf)) cameraSettingValues.Pan = v;
                if (Control.GetCameraControl(CameraControlProperty.Tilt, out v, out cf)) cameraSettingValues.Tilt = v;

                if (Control.GetProcAmp(VideoProcAmpProperty.Brightness, out v, out pf)) cameraSettingValues.Brightness = v;
                if (Control.GetProcAmp(VideoProcAmpProperty.Contrast, out v, out pf)) cameraSettingValues.Contrast = v;
                if (Control.GetProcAmp(VideoProcAmpProperty.Saturation, out v, out pf)) cameraSettingValues.Saturation = v;
                if (Control.GetProcAmp(VideoProcAmpProperty.Hue, out v, out pf)) cameraSettingValues.Hue = v;
                if (Control.GetProcAmp(VideoProcAmpProperty.Sharpness, out v, out pf)) cameraSettingValues.Sharpness = v;
                if (Control.GetProcAmp(VideoProcAmpProperty.Gamma, out v, out pf)) cameraSettingValues.Gamma = v;
                if (Control.GetProcAmp(VideoProcAmpProperty.BacklightCompensation, out v, out pf)) cameraSettingValues.BacklightComp = v;
                if (Control.GetProcAmp(VideoProcAmpProperty.Gain, out v, out pf)) cameraSettingValues.Gain = v;
                if (Control.GetProcAmp(VideoProcAmpProperty.WhiteBalance, out v, out pf))
                {
                    cameraSettingValues.WhiteBalanceBlueU = v;
                    cameraSettingValues.AutoWhiteBalance = (pf & VideoProcAmpFlags.Auto) == VideoProcAmpFlags.Auto;
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                IsSyncingFromCamera = false;
            }
        }

        // Đọc thông số qua OpenCV (dự phòng khi không mở được DirectShow helper)
        public bool ReadCameraSettingValues()
        {
            if (!_cameraReady || _videoCapture == null) return false;
            IsSyncingFromCamera = true;
            try
            {
                try { cameraSettingValues.Exposure = (int)_videoCapture.Exposure; } catch (Exception) { }
                try { cameraSettingValues.Brightness = (int)_videoCapture.Brightness; } catch (Exception) { }
                try { cameraSettingValues.Contrast = (int)_videoCapture.Contrast; } catch (Exception) { }
                try { cameraSettingValues.Saturation = (int)_videoCapture.Saturation; } catch (Exception) { }
                try { cameraSettingValues.Hue = (int)_videoCapture.Hue; } catch (Exception) { }
                try { cameraSettingValues.WhiteBalanceBlueU = (int)_videoCapture.WhiteBalanceBlueU; } catch (Exception) { }
                try { cameraSettingValues.Sharpness = (int)_videoCapture.Sharpness; } catch (Exception) { }
                try { cameraSettingValues.Focus = (int)_videoCapture.Focus; } catch (Exception) { }
                try { cameraSettingValues.Zoom = (int)_videoCapture.Zoom; } catch (Exception) { }
                try { cameraSettingValues.Gain = (int)_videoCapture.Gain; } catch (Exception) { }
                try { cameraSettingValues.Gamma = (int)_videoCapture.Get(VideoCaptureProperties.Gamma); } catch (Exception) { }
                try { cameraSettingValues.BacklightComp = (int)_videoCapture.Get(VideoCaptureProperties.BackLight); } catch (Exception) { }
                try { cameraSettingValues.Pan = (int)_videoCapture.Get(VideoCaptureProperties.Pan); } catch (Exception) { }
                try { cameraSettingValues.Tilt = (int)_videoCapture.Get(VideoCaptureProperties.Tilt); } catch (Exception) { }
                try { cameraSettingValues.AutoExposure = _videoCapture.Get(VideoCaptureProperties.AutoExposure) != 0; } catch (Exception) { }
                try { cameraSettingValues.AutoWhiteBalance = _videoCapture.Get(VideoCaptureProperties.AutoWB) != 0; } catch (Exception) { }
                try { cameraSettingValues.AutoFocus = _videoCapture.Get(VideoCaptureProperties.AutoFocus) != 0; } catch (Exception) { }
                return true;
            }
            finally
            {
                IsSyncingFromCamera = false;
            }
        }

        // Ghi MỘT thông số vừa đổi (kéo slider / tick Auto) lên camera thay vì ghi cả bộ:
        // mỗi lệnh Set là một USB control transfer, ghi cả 14 thông số mỗi lần kéo slider làm UI khựng.
        public bool SetSingle(string propertyName, CameraSettingValues s)
        {
            if (s == null) return false;
            if (!_cameraReady) return false;
            if (string.IsNullOrEmpty(propertyName) || !Control.IsOpen) return SetParammeter(s);

            switch (propertyName)
            {
                case nameof(CameraSettingValues.Exposure):
                case nameof(CameraSettingValues.AutoExposure):
                    return Control.SetCameraControl(CameraControlProperty.Exposure, s.Exposure, s.AutoExposure);
                case nameof(CameraSettingValues.Focus):
                case nameof(CameraSettingValues.AutoFocus):
                    return Control.SetCameraControl(CameraControlProperty.Focus, s.Focus, s.AutoFocus);
                case nameof(CameraSettingValues.Zoom):
                    return Control.SetCameraControl(CameraControlProperty.Zoom, s.Zoom);
                case nameof(CameraSettingValues.Pan):
                    return Control.SetCameraControl(CameraControlProperty.Pan, s.Pan);
                case nameof(CameraSettingValues.Tilt):
                    return Control.SetCameraControl(CameraControlProperty.Tilt, s.Tilt);
                case nameof(CameraSettingValues.Brightness):
                    return Control.SetProcAmp(VideoProcAmpProperty.Brightness, s.Brightness);
                case nameof(CameraSettingValues.Contrast):
                    return Control.SetProcAmp(VideoProcAmpProperty.Contrast, s.Contrast);
                case nameof(CameraSettingValues.Saturation):
                    return Control.SetProcAmp(VideoProcAmpProperty.Saturation, s.Saturation);
                case nameof(CameraSettingValues.Hue):
                    return Control.SetProcAmp(VideoProcAmpProperty.Hue, s.Hue);
                case nameof(CameraSettingValues.Sharpness):
                    return Control.SetProcAmp(VideoProcAmpProperty.Sharpness, s.Sharpness);
                case nameof(CameraSettingValues.Gamma):
                    return Control.SetProcAmp(VideoProcAmpProperty.Gamma, s.Gamma);
                case nameof(CameraSettingValues.BacklightComp):
                    return Control.SetProcAmp(VideoProcAmpProperty.BacklightCompensation, s.BacklightComp);
                case nameof(CameraSettingValues.Gain):
                    return Control.SetProcAmp(VideoProcAmpProperty.Gain, s.Gain);
                case nameof(CameraSettingValues.WhiteBalanceBlueU):
                case nameof(CameraSettingValues.AutoWhiteBalance):
                    return Control.SetProcAmp(VideoProcAmpProperty.WhiteBalance, s.WhiteBalanceBlueU, s.AutoWhiteBalance);
                default:
                    return false;
            }
        }

        // Ghi toàn bộ bộ thông số lên camera. Ưu tiên DirectShow (có cờ Manual/Auto, tự kẹp vào dải hợp lệ),
        // dự phòng OpenCV khi helper chưa mở.
        public bool SetParammeter(CameraSettingValues s)
        {
            if (s == null) return false;

            if (Control.IsOpen)
            {
                // Nhóm Camera Control: đặt Auto/Manual đúng theo checkbox
                Control.SetCameraControl(CameraControlProperty.Exposure, s.Exposure, s.AutoExposure);
                Control.SetCameraControl(CameraControlProperty.Focus, s.Focus, s.AutoFocus);
                Control.SetCameraControl(CameraControlProperty.Zoom, s.Zoom);
                // Pan/Tilt: ảnh trong app đã xoay 90° nên "ngang/dọc" trên UI được map ở VisionPage.xaml
                Control.SetCameraControl(CameraControlProperty.Pan, s.Pan);
                Control.SetCameraControl(CameraControlProperty.Tilt, s.Tilt);

                // Nhóm Video Proc Amp
                Control.SetProcAmp(VideoProcAmpProperty.Brightness, s.Brightness);
                Control.SetProcAmp(VideoProcAmpProperty.Contrast, s.Contrast);
                Control.SetProcAmp(VideoProcAmpProperty.Saturation, s.Saturation);
                Control.SetProcAmp(VideoProcAmpProperty.Hue, s.Hue);
                Control.SetProcAmp(VideoProcAmpProperty.Sharpness, s.Sharpness);
                Control.SetProcAmp(VideoProcAmpProperty.Gamma, s.Gamma);
                Control.SetProcAmp(VideoProcAmpProperty.BacklightCompensation, s.BacklightComp);
                Control.SetProcAmp(VideoProcAmpProperty.Gain, s.Gain);
                Control.SetProcAmp(VideoProcAmpProperty.WhiteBalance, s.WhiteBalanceBlueU, s.AutoWhiteBalance);
                return true;
            }

            if (!_cameraReady || _videoCapture == null) return false;

            try { _videoCapture.Exposure = s.Exposure; } catch (Exception) { }
            try { _videoCapture.Brightness = s.Brightness; } catch (Exception) { }
            try { _videoCapture.Contrast = s.Contrast; } catch (Exception) { }
            try { _videoCapture.Saturation = s.Saturation; } catch (Exception) { }
            try { _videoCapture.Hue = s.Hue; } catch (Exception) { }
            try { _videoCapture.WhiteBalanceBlueU = s.WhiteBalanceBlueU; } catch (Exception) { }
            try { _videoCapture.Sharpness = s.Sharpness; } catch (Exception) { }
            try { _videoCapture.Focus = s.Focus; } catch (Exception) { }
            try { _videoCapture.Zoom = s.Zoom; } catch (Exception) { }
            try { _videoCapture.Gain = s.Gain; } catch (Exception) { }
            try { _videoCapture.Set(VideoCaptureProperties.Gamma, s.Gamma); } catch (Exception) { }
            try { _videoCapture.Set(VideoCaptureProperties.BackLight, s.BacklightComp); } catch (Exception) { }
            try { _videoCapture.Set(VideoCaptureProperties.Pan, s.Pan); } catch (Exception) { }
            try { _videoCapture.Set(VideoCaptureProperties.Tilt, s.Tilt); } catch (Exception) { }
            try { _videoCapture.Set(VideoCaptureProperties.AutoExposure, s.AutoExposure ? 1 : 0); } catch (Exception) { }
            try { _videoCapture.Set(VideoCaptureProperties.AutoWB, s.AutoWhiteBalance ? 1 : 0); } catch (Exception) { }
            try { _videoCapture.Set(VideoCaptureProperties.AutoFocus, s.AutoFocus ? 1 : 0); } catch (Exception) { }
            return true;
        }

    }

    // Giá trị mặc định = bộ thông số Logitech BRIO đã tinh chỉnh (Video Proc Amp + Camera Control, tất cả Auto = off)
    public class CameraSettingValues : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private int brightness = 156;

        public int Brightness
        {
            get
            {
                return brightness;
            }

            set
            {
                brightness = value;
                NotifyPropertyChanged(nameof(Brightness));
            }
        }

        private int contrast = 125;

        public int Contrast
        {
            get
            {
                return contrast;
            }

            set
            {
                contrast = value;
                NotifyPropertyChanged(nameof(Contrast));
            }
        }

        private int saturation = 179;

        public int Saturation
        {
            get
            {
                return saturation;
            }

            set
            {
                saturation = value;
                NotifyPropertyChanged(nameof(Saturation));
            }
        }

        private int hue = 0;

        public int Hue
        {
            get
            {
                return hue;
            }

            set
            {
                hue = value;
                NotifyPropertyChanged(nameof(Hue));
            }
        }

        private int sharpness = 255;

        public int Sharpness
        {
            get
            {
                return sharpness;
            }

            set
            {
                sharpness = value;
                NotifyPropertyChanged(nameof(Sharpness));
            }
        }

        private int whiteBalanceBlueU = 7500;

        public int WhiteBalanceBlueU
        {
            get
            {
                return whiteBalanceBlueU;
            }

            set
            {
                whiteBalanceBlueU = value;
                NotifyPropertyChanged(nameof(WhiteBalanceBlueU));
            }
        }


        private int backlightComp = 0;

        public int BacklightComp
        {
            get
            {
                return backlightComp;
            }

            set
            {
                backlightComp = value;
                NotifyPropertyChanged(nameof(BacklightComp));
            }
        }

        private int gain = 0;

        public int Gain
        {
            get
            {
                return gain;
            }

            set
            {
                gain = value;
                NotifyPropertyChanged(nameof(Gain));
            }
        }

        private int zoom = 129;

        public int Zoom
        {
            get
            {
                return zoom;
            }

            set
            {
                zoom = value;
                NotifyPropertyChanged(nameof(Zoom));
            }
        }

        private int focus = 20;

        public int Focus
        {
            get
            {
                return focus;
            }

            set
            {
                focus = value;
                NotifyPropertyChanged(nameof(Focus));
            }
        }

        private int exposure = -10;

        public int Exposure
        {
            get
            {
                return exposure;
            }

            set
            {
                exposure = value;
                NotifyPropertyChanged(nameof(Exposure));
            }
        }

        private int pan = -8;

        public int Pan
        {
            get
            {
                return pan;
            }

            set
            {
                pan = value;
                NotifyPropertyChanged(nameof(Pan));
            }
        }

        private int tilt = 10;

        public int Tilt
        {
            get
            {
                return tilt;
            }

            set
            {
                tilt = value;
                NotifyPropertyChanged(nameof(Tilt));
            }
        }

        private int gamma = 100;

        public int Gamma
        {
            get
            {
                return gamma;
            }

            set
            {
                gamma = value;
                NotifyPropertyChanged(nameof(Gamma));
            }
        }

        private bool autoExposure = false;

        public bool AutoExposure
        {
            get
            {
                return autoExposure;
            }

            set
            {
                autoExposure = value;
                NotifyPropertyChanged(nameof(AutoExposure));
            }
        }

        private bool autoWhiteBalance = false;

        public bool AutoWhiteBalance
        {
            get
            {
                return autoWhiteBalance;
            }

            set
            {
                autoWhiteBalance = value;
                NotifyPropertyChanged(nameof(AutoWhiteBalance));
            }
        }

        private bool autoFocus = false;

        public bool AutoFocus
        {
            get
            {
                return autoFocus;
            }

            set
            {
                autoFocus = value;
                NotifyPropertyChanged(nameof(AutoFocus));
            }
        }

        // Sao chép toàn bộ giá trị từ một bộ thông số khác (kích hoạt cập nhật UI + camera)
        public void CopyFrom(CameraSettingValues s)
        {
            if (s == null) return;
            Exposure = s.Exposure;
            Brightness = s.Brightness;
            Contrast = s.Contrast;
            Saturation = s.Saturation;
            Hue = s.Hue;
            Gamma = s.Gamma;
            Sharpness = s.Sharpness;
            BacklightComp = s.BacklightComp;
            WhiteBalanceBlueU = s.WhiteBalanceBlueU;
            Focus = s.Focus;
            Zoom = s.Zoom;
            Gain = s.Gain;
            Pan = s.Pan;
            Tilt = s.Tilt;
            AutoExposure = s.AutoExposure;
            AutoWhiteBalance = s.AutoWhiteBalance;
            AutoFocus = s.AutoFocus;
        }

    }
}