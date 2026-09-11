using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace LEDVision
{
    public class DeviceControl
    {
        public event EventHandler OnStartRequest;
        public event EventHandler OnCancleRequest;

        public SerialPort port;

        public string comPort;

        public bool TriggerTest = false;

        // Bỏ qua tín hiệu reed switch (start / cancel) trong lúc app tự điều khiển xi lanh (retry, kết thúc test).
        // Xi lanh lên xuống do app điều khiển cũng tạo cạnh reed y như người vận hành, phải chặn.
        public volatile bool IgnoreTrigger = false;

        // Khung 4 byte hai chiều: [cmd][key][value][FRAME_END]
        public const byte FRAME_END = 0x11;
        public const byte CMD_SET = 0x52;    // PC → board: đặt một ngõ ra (key 0..7, value 0/1). Board echo lại.
        public const byte CMD_GET = 0x49;    // PC → board: đọc input (key 0 tất cả / 1 UP / 2 DOWN). Board → PC: sự kiện cảm biến.
        public const byte CMD_RESET = 0xD5;  // PC → board: tắt mọi ngõ ra. Board echo lại.
        public const byte IN_UP = 1, IN_DOWN = 2;


        // Chỉ true khi CheckCommunication mở cổng thành công; về false khi ghi lỗi (rớt USB...).
        // Mọi lệnh gửi đều kiểm tra cờ này → cổng chưa mở / đã rớt thì KHÔNG gửi, không chờ timeout, không đơ UI.
        public bool IsConnected { get; private set; } = false;

        private const int PortTimeoutMs = 500;

        // true trong lúc đang TEST: ghi bị timeout cũng coi là mất kết nối (board phải trả lời khi test).
        // false lúc rảnh: timeout chỉ bỏ qua gói (tránh icon COM tắt xanh khi chuyển trang).
        public bool TreatTimeoutAsDisconnect { get; set; } = false;

        public DeviceControl()
        {
            port = new SerialPort()
            {
                BaudRate = 9600,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                ReceivedBytesThreshold = 1,
                WriteTimeout = PortTimeoutMs,
                ReadTimeout = PortTimeoutMs
            };
        }


        // state define
        public const bool ON = true;

        public const bool OFF = false;

        /// <summary>
        /// Software sw control main cylinder going up
        /// </summary>
        /// 
        private bool _CylinderUp;

        public bool CylinderUp
        {
            get { return _CylinderUp; }
            set
            {
                if (value != _CylinderUp) _CylinderUp = value;
            }
        }

        private bool _CylinderDown;

        public bool CylinderDown
        {
            get { return _CylinderDown; }
            set
            {
                if (value != _CylinderDown) _CylinderDown = value;
            }
        }

        // 5 relay đa dụng. Mọi ngõ ra (Power / Up / Down / RL1..5) đều gửi MỖI FRAME MỘT NGÕ RA bằng lệnh 0x52 [index][state]:
        //   0 = Power, 1 = Up, 2 = Down, 3..7 = RL1..RL5
        public bool[] Relay = new bool[5];
        public const int OUT_POWER = 0, OUT_UP = 1, OUT_DOWN = 2, OUT_RELAY1 = 3;

        private static byte[] Frame(byte cmd, byte key, byte value)
        {
            return new byte[] { cmd, key, value, FRAME_END };
        }

        // Trạng thái Power / Up / Down đã gửi lần cuối → SendControl chỉ gửi ngõ nào vừa đổi
        private readonly bool[] lastSent = new bool[3];
        private bool lastSentValid = false;

        // Gửi một ngõ ra. Trả về true nếu gửi được (cổng mở và không lỗi).
        public bool SetOutput(int index, bool on)
        {
            if (index < 0 || index > 7) return false;
            if (!IsConnected || port == null || !port.IsOpen) return false;
            SendBytes(Frame(CMD_SET, (byte)index, (byte)(on ? 1 : 0)));
            return IsConnected;
        }

        // Hỏi board trạng thái input hiện tại (board trả lời một frame cho mỗi input)
        public void RequestInputs()
        {
            if (!IsConnected || port == null || !port.IsOpen) return;
            SendBytes(Frame(CMD_GET, 0, 0));
        }

        // Tắt mọi ngõ ra trên board bằng một frame
        public void ResetOutputs()
        {
            if (!IsConnected || port == null || !port.IsOpen) return;
            SendBytes(Frame(CMD_RESET, 0, 0));
            for (int i = 0; i < Relay.Length; i++) Relay[i] = false;
            lastSentValid = false;
        }

        // Relay: mỗi relay một frame, gửi chặt (lỗi / timeout / cổng chưa mở = mất kết nối)
        public bool SetRelay(int index, bool on)
        {
            if (index < 0 || index >= Relay.Length) return false;
            Relay[index] = on;
            if (!IsConnected || port == null || !port.IsOpen)
            {
                IsConnected = false;
                return false;
            }
            bool prev = TreatTimeoutAsDisconnect;
            TreatTimeoutAsDisconnect = true;
            try
            {
                return SetOutput(OUT_RELAY1 + index, on);
            }
            finally
            {
                TreatTimeoutAsDisconnect = prev;
            }
        }

        // Tắt cả 5 relay, mỗi cái một frame
        public void AllRelaysOff()
        {
            for (int i = 0; i < Relay.Length; i++)
            {
                if (Relay[i]) SetRelay(i, false);
            }
        }

        // Gửi Power / Up / Down: mỗi ngõ một frame, chỉ gửi ngõ nào khác lần gửi trước (lần đầu gửi cả 3)
        private void SendChangedOutputs()
        {
            bool[] now = { Power, CylinderUp, CylinderDown };
            for (int i = 0; i < 3; i++)
            {
                if (!lastSentValid || now[i] != lastSent[i])
                {
                    if (!SetOutput(i, now[i])) return;   // mất kết nối giữa chừng → lần sau gửi lại đủ
                    lastSent[i] = now[i];
                }
            }
            lastSentValid = true;
        }

        private bool _Power;

        public bool Power
        {
            get { return _Power; }
            set
            {
                _Power = value;
            }
        }


        /// <summary>
        /// Sensor main cylinder upstate
        /// </summary>
        private bool _SS_UP;

        public bool SS_UP
        {
            get { return _SS_UP; }
            set
            {
                if (value != _SS_UP) _SS_UP = value;
            }
        }

        /// <summary>
        /// Sensor main cylinder down state
        /// </summary>
        private bool _SS_DOWN;

        public bool SS_DOWN
        {
            get { return _SS_DOWN; }
            set
            {
                if (value != _SS_DOWN)
                {
                    if (_SS_DOWN == OFF)
                    {
                        Task.Delay(100).Wait();
                        OnStartRequest?.Invoke(null, null);
                    }
                    if (_SS_DOWN == ON)
                    {
                        Task.Delay(100).Wait();
                        OnCancleRequest?.Invoke(null, null);

                    }
                    _SS_DOWN = value;
                }

            }
        }

        // Ngắt kết nối chủ động (nút Disconnect ở trang Setting): đóng cổng, đèn về vàng
        public void Disconnect(Rectangle statusLamp)
        {
            IsConnected = false;
            lastSentValid = false;
            try
            {
                if (port != null && port.IsOpen)
                {
                    port.DataReceived -= Port_DataReceived;
                    port.Close();
                }
            }
            catch (Exception)
            {
            }
            if (statusLamp != null) statusLamp.Fill = System.Windows.Media.Brushes.Yellow;
        }

        // Chờ cảm biến DOWN báo xi lanh đã xuống hẳn (thay cho chờ cố định). Trả về false nếu quá timeout.
        // Cổng chưa mở thì không có gì để chờ → trả về true ngay để chuỗi test vẫn chạy được khi thử tay.
        public bool WaitForDown(int timeoutMs)
        {
            if (!IsConnected) return true;
            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalMilliseconds < timeoutMs)
            {
                if (SS_DOWN) return true;
                if (VisionTest.CancelRequested) return false;   // hủy → không chờ nữa
                System.Threading.Thread.Sleep(20);
            }
            return SS_DOWN;
        }

        // Chờ cảm biến DOWN nhả (xi lanh đã rời vị trí dưới). Chỉ có một cảm biến nên đây là dấu hiệu "đang lên".
        public bool WaitForLeaveDown(int timeoutMs)
        {
            if (!IsConnected) return true;
            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalMilliseconds < timeoutMs)
            {
                if (!SS_DOWN) return true;
                if (VisionTest.CancelRequested) return false;   // hủy → không chờ nữa
                System.Threading.Thread.Sleep(20);
            }
            return !SS_DOWN;
        }

        public void CheckCommunication(Rectangle statusLamp)
        {
            if (port != null)
            {
                if (comPort == null || comPort == "")
                {
                    return;
                }
                if (port.IsOpen)
                {
                    port.Close();
                    var startClosePortTime = DateTime.Now;
                    while (port.IsOpen)
                    {
                        if (DateTime.Now.Subtract(startClosePortTime).TotalMilliseconds > 500)
                        {
                            break;
                        }
                    }
                }

                IsConnected = false;
                port = new SerialPort()
                {
                    PortName = comPort,
                    BaudRate = 9600,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    ReceivedBytesThreshold = 1,
                    WriteTimeout = PortTimeoutMs,   // mặc định là vô hạn → Write có thể treo UI khi thiết bị rớt
                    ReadTimeout = PortTimeoutMs
                };

                lastSentValid = false;
                try
                {
                    port.Open();
                    IsConnected = true;
                    rxBuf.Clear();
                    statusLamp.Fill = (Brush)new BrushConverter().ConvertFromString("#06C755");
                    port.DataReceived -= Port_DataReceived;
                    port.DataReceived += Port_DataReceived;
                    RequestInputs();   // biết ngay xi lanh đang ở đâu
                }
                catch (Exception)
                {
                    statusLamp.Fill = System.Windows.Media.Brushes.Red;

                }
            }
        }

        public void SendBytes(byte[] buf)
        {
            // Cổng chưa mở (hoặc đã rớt) → không gửi
            if (!IsConnected || port == null || !port.IsOpen) { return; }

            try
            {
                port.Write(buf, 0, buf.Length);
            }
            catch (TimeoutException)
            {
                // Lúc rảnh: board bận / chưa đọc kịp → bỏ qua gói này, giữ kết nối.
                // Lúc đang test: coi là mất kết nối để báo ngay.
                if (TreatTimeoutAsDisconnect)
                {
                    IsConnected = false;
                    try { port.Close(); } catch (Exception) { }
                }
            }
            catch (Exception)
            {
                // IOException / InvalidOperationException: cổng thật sự rớt (rút USB, cổng bị đóng) → mất kết nối
                IsConnected = false;
                try { port.Close(); } catch (Exception) { }
            }

        }

        public void SendControl()
        {
            if (!IsConnected || port == null || !port.IsOpen) return;
            SendChangedOutputs();
        }

        // Gửi "chặt" cho thao tác chủ động (nút Power / Up / Down): cổng chưa mở hoặc ghi lỗi, kể cả timeout,
        // đều coi là mất kết nối. Trả về true nếu gửi được.
        public bool SendControlStrict()
        {
            if (!IsConnected || port == null || !port.IsOpen)
            {
                IsConnected = false;
                return false;
            }
            bool prev = TreatTimeoutAsDisconnect;
            TreatTimeoutAsDisconnect = true;
            try
            {
                SendChangedOutputs();
            }
            finally
            {
                TreatTimeoutAsDisconnect = prev;
            }
            return IsConnected;
        }

        // Bộ đệm nhận: khung 4 byte [cmd][key][value][0x11]. Byte lẻ (không kết thúc 0x11) bị bỏ từng byte để đồng bộ lại.
        private readonly List<byte> rxBuf = new List<byte>();

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!IsConnected || port == null || !port.IsOpen) return;
            byte[] bytes;
            try
            {
                int n = port.BytesToRead;
                if (n <= 0) return;
                bytes = new byte[n];
                port.Read(bytes, 0, n);
            }
            catch (Exception)
            {
                return;
            }

            rxBuf.AddRange(bytes);
            while (rxBuf.Count >= 4)
            {
                if (rxBuf[3] != FRAME_END)
                {
                    rxBuf.RemoveAt(0);   // lệch khung → bỏ 1 byte, thử lại
                    continue;
                }
                byte cmd = rxBuf[0], key = rxBuf[1], value = rxBuf[2];
                rxBuf.RemoveRange(0, 4);
                Console.WriteLine("IOBOX RX: " + cmd.ToString("X2") + " " + key.ToString("X2") + " " + value.ToString("X2"));

                if (cmd == CMD_GET)
                {
                    // Trạng thái cảm biến (sự kiện hoặc trả lời GET). SS_DOWN setter tự phát start / cancel theo cạnh.
                    if (key == IN_UP) SS_UP = value != 0;
                    else if (key == IN_DOWN) SS_DOWN = value != 0;
                }
                // CMD_SET / CMD_RESET echo: không cần xử lý
            }
        }
    }
}