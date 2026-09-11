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

        public static byte Prefix1 = 0x44;
        public static byte Prefix2 = 0x45;
        public static byte Suffix = 0x56;


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
        private const byte CMD_SET_OUTPUT = 0x52;
        public const int OUT_POWER = 0, OUT_UP = 1, OUT_DOWN = 2, OUT_RELAY1 = 3;

        // Trạng thái Power / Up / Down đã gửi lần cuối → SendControl chỉ gửi ngõ nào vừa đổi
        private readonly bool[] lastSent = new bool[3];
        private bool lastSentValid = false;

        // Gửi một ngõ ra. Trả về true nếu gửi được (cổng mở và không lỗi).
        public bool SetOutput(int index, bool on)
        {
            if (index < 0 || index > 7) return false;
            if (!IsConnected || port == null || !port.IsOpen) return false;
            // 5 byte dữ liệu như gói 0x4F để frame giữ đúng 10 byte firmware đang chờ
            byte[] data = { CMD_SET_OUTPUT, (byte)index, (byte)(on ? 1 : 0), 0x00, 0x00 };
            SendBytes(GetFrame(data));
            return IsConnected;
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
                    statusLamp.Fill = (Brush)new BrushConverter().ConvertFromString("#06C755");
                    port.DataReceived -= Port_DataReceived;
                    port.DataReceived += Port_DataReceived;
                }
                catch (Exception)
                {
                    statusLamp.Fill = System.Windows.Media.Brushes.Red;

                }
            }
        }


        public void DataToIO(byte[] bytes)
        {
            if (bytes.Length != 4)
            {
                return;
            }
            UInt32 Data32Bit = BitConverter.ToUInt32(bytes, 0);

            SS_UP = GetValue(Data32Bit, 8);
            SS_DOWN = GetValue(Data32Bit, 9);
        }

        public bool GetValue(UInt32 data, int position)
        {
            return (data & (UInt32)(1 << position)) != 0;
        }

        public byte CalculateCheckSum(byte[] byteData) //Dis
        {
            Byte chkSumByte = 0x00;
            for (int i = 0; i < byteData.Length; i++)
                chkSumByte ^= byteData[i];
            return chkSumByte;
        }

        public byte[] GetFrame(byte[] datas, bool IsNoSize = false)
        {

            if (datas == null) return null;

            List<byte> dataToSend = datas.ToList();
            if (!IsNoSize)
            {
                if (datas.Length > 1)
                {
                    dataToSend.Insert(0, (byte)(dataToSend.Count + 1));
                }
                else
                {
                    dataToSend.Add(0x00);
                }
            }
            dataToSend.Insert(0, Prefix2);
            dataToSend.Insert(0, Prefix1);
            var checksum = CalculateCheckSum(dataToSend.ToArray());
            dataToSend.Add(checksum);
            dataToSend.Add(Suffix);

            //dataToSend.Insert(2, (Byte)(dataToSend.Count - 3) );
            foreach (var item in dataToSend)
            {
                Console.Write(item.ToString("X2") + " ");
            }
            Console.WriteLine(" ");
            return dataToSend.ToArray();
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

        public byte[] IOtoData()
        {
            byte[] bytes = new byte[5];
            bytes[0] = 0x4F;
            List<bool> OutPuts = new List<bool>
            {
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,

                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,

                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,

                Power,
                CylinderUp,
                CylinderDown,
                false,
                false,
                false,
                false,
                false
            };

            BitArray bits = new BitArray(OutPuts.ToArray());
            bits.CopyTo(bytes, 1);
            return bytes;
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

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (IsConnected && port != null && port.IsOpen)
            {
                List<byte> frame = new List<byte>();
                Task.Delay(50).Wait();
                int size = port.BytesToRead;
                byte[] bytes = new byte[size];
                try
                {
                    port.Read(bytes, 0, port.BytesToRead);
                }
                catch (Exception)
                {
                    return;
                }

                if (bytes.Length < 7) return;
                for (int i = 0; i < bytes.Length; i++)
                {
                    byte startByte = bytes[i];
                    if (startByte == Prefix1)
                    {
                        var secondByte = bytes[i + 1];
                        if (secondByte == Prefix2)
                        {
                            frame.Clear();
                            frame.Add(startByte);
                            frame.Add(secondByte);
                            frame.Add(bytes[i + 2]);

                            if ((int)bytes[i + 2] + 3 >= bytes.Length) return;

                            for (int j = i + 3; j <= (int)bytes[i + 2] + 3; j++)
                            {
                                frame.Add(bytes[j]);
                            }
                            try
                            {
                                DataToIO(new byte[] { frame[4], frame[5], frame[6], frame[7] });
                            }
                            catch (Exception ex)
                            {
                            }

                            Console.Write("SYS INPUT:");
                            foreach (var item in frame)
                            {
                                Console.Write(item.ToString("X2") + " ");
                            }
                            Console.WriteLine(" ");
                            return;

                        }
                    }
                }
            }
        }
    }
}