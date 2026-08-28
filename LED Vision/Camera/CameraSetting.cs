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

        // Chọn đúng camera: ưu tiên Logitech, tránh camera ảo (OBS/Virtual). Mặc định index 0.
        private int FindCameraIndex()
        {
            try
            {
                var devices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
                if (devices != null && devices.Length > 0)
                {
                    for (int i = 0; i < devices.Length; i++)
                    {
                        var name = devices[i].Name ?? "";
                        if (name.IndexOf("Logitech", StringComparison.OrdinalIgnoreCase) >= 0)
                            return i;
                    }
                    for (int i = 0; i < devices.Length; i++)
                    {
                        var name = devices[i].Name ?? "";
                        if (name.IndexOf("OBS", StringComparison.OrdinalIgnoreCase) < 0 &&
                            name.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) < 0)
                            return i;
                    }
                }
            }
            catch (Exception)
            {
            }
            return 0;
        }

        public async Task StartCamera()
        {
            if (_previewTask != null && !_previewTask.IsCompleted) return;  // Prevent reinitializing the task if already running

            _cancellationTokenSource = new CancellationTokenSource();
            _previewTask = Task.Run(async () =>
            {
                try
                {
                    _videoCapture = new VideoCapture(FindCameraIndex(), VideoCaptureAPIs.DSHOW);
                    try
                    {
                        _videoCapture.BufferSize = 1;
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

        // Đọc thông số hiện tại từ camera vào bộ thông số dùng chung
        public void ReadCameraSettingValues()
        {
            try
            {
                cameraSettingValues.Exposure = (int)_videoCapture.Exposure;
            }
            catch (Exception ex)
            {

            }
            try
            {
                cameraSettingValues.Brightness = (int)_videoCapture.Brightness;
            }
            catch (Exception ex)
            {

            }
            try
            {
                cameraSettingValues.Contrast = (int)_videoCapture.Contrast;
            }
            catch (Exception ex)
            {

            }
            try
            {
                cameraSettingValues.Saturation = (int)_videoCapture.Saturation;
            }
            catch (Exception ex)
            {
            }
            try
            {
                cameraSettingValues.Hue = (int)_videoCapture.Hue;
            }
            catch (Exception ex)
            {

            }
            try
            {
                cameraSettingValues.WhiteBalanceBlueU = (int)_videoCapture.WhiteBalanceBlueU;
            }
            catch (Exception ex)
            {

            }
            try
            {
                cameraSettingValues.Sharpness = (int)_videoCapture.Sharpness;
            }
            catch (Exception ex)
            {

            }
            try
            {
                cameraSettingValues.Focus = (int)_videoCapture.Focus;

            }
            catch (Exception ex)
            {
            }
            try
            {
                cameraSettingValues.Zoom = (int)_videoCapture.Zoom;
            }
            catch (Exception ex)
            {

            }
            try
            {
                cameraSettingValues.Gain = (int)_videoCapture.Gain;
            }
            catch (Exception ex)
            {

            }
            try
            {
                cameraSettingValues.Gamma = (int)_videoCapture.Get(VideoCaptureProperties.Gamma);
            }
            catch (Exception ex)
            {

            }
            try
            {
                cameraSettingValues.BacklightComp = (int)_videoCapture.Get(VideoCaptureProperties.BackLight);
            }
            catch (Exception ex)
            {

            }
            try
            {
                cameraSettingValues.Pan = (int)_videoCapture.Get(VideoCaptureProperties.Pan);
            }
            catch (Exception ex)
            {

            }
            try
            {
                cameraSettingValues.Tilt = (int)_videoCapture.Get(VideoCaptureProperties.Tilt);
            }
            catch (Exception ex)
            {

            }
            try
            {
                cameraSettingValues.AutoExposure = _videoCapture.Get(VideoCaptureProperties.AutoExposure) != 0;
            }
            catch (Exception ex)
            {

            }
            try
            {
                cameraSettingValues.AutoWhiteBalance = _videoCapture.Get(VideoCaptureProperties.AutoWB) != 0;
            }
            catch (Exception ex)
            {

            }
            try
            {
                cameraSettingValues.AutoFocus = _videoCapture.Get(VideoCaptureProperties.AutoFocus) != 0;
            }
            catch (Exception ex)
            {

            }
        }
               
        public bool SetParammeter(CameraSettingValues NewCameraSetting)
        {
            if (NewCameraSetting != null)
            {
                try
                {
                    _videoCapture.Exposure = NewCameraSetting.Exposure;
                }
                catch (Exception ex)
                {

                }
                try
                {
                    _videoCapture.Brightness = NewCameraSetting.Brightness;
                }
                catch (Exception ex)
                {

                }

                try
                {
                    _videoCapture.Contrast = NewCameraSetting.Contrast;
                }
                catch (Exception ex)
                {

                }
                try
                {
                    _videoCapture.Saturation = NewCameraSetting.Saturation;
                }
                catch (Exception ex)
                {

                }

                try
                {
                    _videoCapture.Hue = NewCameraSetting.Hue;
                }
                catch (Exception ex)
                {

                }

                try
                {
                    _videoCapture.WhiteBalanceBlueU = NewCameraSetting.WhiteBalanceBlueU;
                }
                catch (Exception ex)
                {

                }

                try
                {
                    _videoCapture.Sharpness = NewCameraSetting.Sharpness;
                }
                catch (Exception ex)
                {

                }
                try
                {
                    _videoCapture.Focus = NewCameraSetting.Focus;
                }
                catch (Exception ex)
                {

                }

                try
                {
                    _videoCapture.Zoom = NewCameraSetting.Zoom;
                }
                catch (Exception ex)
                {

                }
                try
                {
                    _videoCapture.Gain = NewCameraSetting.Gain;
                }
                catch (Exception ex)
                {

                }

                try
                {
                    _videoCapture.Set(VideoCaptureProperties.Gamma, NewCameraSetting.Gamma);
                }
                catch (Exception ex)
                {

                }

                try
                {
                    _videoCapture.Set(VideoCaptureProperties.BackLight, NewCameraSetting.BacklightComp);
                }
                catch (Exception ex)
                {

                }

                try
                {
                    _videoCapture.Set(VideoCaptureProperties.Pan, NewCameraSetting.Pan);
                }
                catch (Exception ex)
                {

                }

                try
                {
                    _videoCapture.Set(VideoCaptureProperties.Tilt, NewCameraSetting.Tilt);
                }
                catch (Exception ex)
                {

                }

                try
                {
                    _videoCapture.Set(VideoCaptureProperties.AutoExposure, NewCameraSetting.AutoExposure ? 1 : 0);
                }
                catch (Exception ex)
                {

                }

                try
                {
                    _videoCapture.Set(VideoCaptureProperties.AutoWB, NewCameraSetting.AutoWhiteBalance ? 1 : 0);
                }
                catch (Exception ex)
                {

                }

                try
                {
                    _videoCapture.Set(VideoCaptureProperties.AutoFocus, NewCameraSetting.AutoFocus ? 1 : 0);
                }
                catch (Exception ex)
                {

                }


            }
            return true;
        }

    }

    public class CameraSettingValues : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private int brightness = 111;

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

        private int contrast = 51;

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

        private int saturation = 255;

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

        private int whiteBalanceBlueU = 2990;

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

        private int exposure = -8;

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

        private int pan = 0;

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

        private int tilt = 0;

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