using DirectShowLib;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LEDVision.Camera
{
    // Dải giá trị của một thông số camera (đọc từ driver qua DirectShow)
    public class CameraPropertyRange
    {
        public int Min;
        public int Max;
        public int Step;
        public int Default;

        public int Clamp(int value)
        {
            if (value < Min) value = Min;
            if (value > Max) value = Max;
            if (Step > 1)
            {
                // Làm tròn về bước gần nhất tính từ Min
                value = Min + (int)Math.Round((value - Min) / (double)Step) * Step;
                if (value > Max) value = Max;
            }
            return value;
        }

        public override string ToString()
        {
            return "[" + Min + ".." + Max + " step " + Step + " def " + Default + "]";
        }
    }

    /// <summary>
    /// Truy cập trực tiếp IAMCameraControl / IAMVideoProcAmp của camera qua DirectShow.
    /// Dùng để:
    ///  - Đọc dải giá trị thật (min/max/step) của từng thông số → UI đặt Min/Max slider đúng với camera.
    ///  - Set Pan/Tilt với cờ Manual (OpenCV DSHOW set Pan/Tilt không ổn định trên nhiều driver Logitech).
    /// Mở một filter riêng cho cùng thiết bị: driver UVC cho phép đổi thông số ngay cả khi
    /// OpenCV đang giữ luồng ảnh (giống cách phần mềm Logitech chỉnh camera khi app khác đang dùng).
    /// </summary>
    public class CameraControlHelper : IDisposable
    {
        private IBaseFilter _filter;
        private IAMCameraControl _cameraControl;
        private IAMVideoProcAmp _procAmp;

        public bool IsOpen
        {
            get { return _filter != null; }
        }

        public Dictionary<CameraControlProperty, CameraPropertyRange> CameraRanges = new Dictionary<CameraControlProperty, CameraPropertyRange>();
        public Dictionary<VideoProcAmpProperty, CameraPropertyRange> ProcAmpRanges = new Dictionary<VideoProcAmpProperty, CameraPropertyRange>();

        // Chọn đúng camera: ưu tiên Logitech, tránh camera ảo (OBS/Virtual). Mặc định index 0.
        public static int FindCameraIndex()
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

        public bool Open(int deviceIndex)
        {
            Close();
            try
            {
                var devices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
                if (devices == null || devices.Length == 0) return false;
                if (deviceIndex < 0 || deviceIndex >= devices.Length) deviceIndex = 0;

                Guid iid = typeof(IBaseFilter).GUID;
                object source;
                devices[deviceIndex].Mon.BindToObject(null, null, ref iid, out source);
                _filter = source as IBaseFilter;
                if (_filter == null) return false;

                _cameraControl = _filter as IAMCameraControl;
                _procAmp = _filter as IAMVideoProcAmp;

                ReadAllRanges();
                return true;
            }
            catch (Exception)
            {
                Close();
                return false;
            }
        }

        private void ReadAllRanges()
        {
            CameraRanges.Clear();
            ProcAmpRanges.Clear();

            if (_cameraControl != null)
            {
                foreach (CameraControlProperty p in Enum.GetValues(typeof(CameraControlProperty)))
                {
                    try
                    {
                        int min, max, step, def;
                        CameraControlFlags flags;
                        int hr = _cameraControl.GetRange(p, out min, out max, out step, out def, out flags);
                        if (hr == 0 && max > min)
                        {
                            CameraRanges[p] = new CameraPropertyRange { Min = min, Max = max, Step = step <= 0 ? 1 : step, Default = def };
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            if (_procAmp != null)
            {
                foreach (VideoProcAmpProperty p in Enum.GetValues(typeof(VideoProcAmpProperty)))
                {
                    try
                    {
                        int min, max, step, def;
                        VideoProcAmpFlags flags;
                        int hr = _procAmp.GetRange(p, out min, out max, out step, out def, out flags);
                        if (hr == 0 && max > min)
                        {
                            ProcAmpRanges[p] = new CameraPropertyRange { Min = min, Max = max, Step = step <= 0 ? 1 : step, Default = def };
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        public CameraPropertyRange GetRange(CameraControlProperty p)
        {
            CameraPropertyRange r;
            return CameraRanges.TryGetValue(p, out r) ? r : null;
        }

        public CameraPropertyRange GetRange(VideoProcAmpProperty p)
        {
            CameraPropertyRange r;
            return ProcAmpRanges.TryGetValue(p, out r) ? r : null;
        }

        // Set thông số nhóm CameraControl (Pan/Tilt/Zoom/Focus/Exposure...) ở chế độ Manual, tự kẹp vào dải hợp lệ.
        public bool SetCameraControl(CameraControlProperty p, int value, bool auto = false)
        {
            if (_cameraControl == null) return false;
            try
            {
                var range = GetRange(p);
                if (range != null) value = range.Clamp(value);
                int hr = _cameraControl.Set(p, value, auto ? CameraControlFlags.Auto : CameraControlFlags.Manual);
                return hr == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool GetCameraControl(CameraControlProperty p, out int value, out CameraControlFlags flags)
        {
            value = 0;
            flags = CameraControlFlags.None;
            if (_cameraControl == null) return false;
            try
            {
                return _cameraControl.Get(p, out value, out flags) == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Set thông số nhóm VideoProcAmp (Brightness/Contrast/WhiteBalance...) Manual/Auto, tự kẹp vào dải hợp lệ.
        public bool SetProcAmp(VideoProcAmpProperty p, int value, bool auto = false)
        {
            if (_procAmp == null) return false;
            try
            {
                var range = GetRange(p);
                if (range != null) value = range.Clamp(value);
                int hr = _procAmp.Set(p, value, auto ? VideoProcAmpFlags.Auto : VideoProcAmpFlags.Manual);
                return hr == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool GetProcAmp(VideoProcAmpProperty p, out int value, out VideoProcAmpFlags flags)
        {
            value = 0;
            flags = VideoProcAmpFlags.None;
            if (_procAmp == null) return false;
            try
            {
                return _procAmp.Get(p, out value, out flags) == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Close()
        {
            _cameraControl = null;
            _procAmp = null;
            if (_filter != null)
            {
                try { Marshal.ReleaseComObject(_filter); } catch (Exception) { }
                _filter = null;
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
