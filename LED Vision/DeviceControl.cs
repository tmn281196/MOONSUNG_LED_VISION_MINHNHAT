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

        public static byte Prefix1 = 0x44;
        public static byte Prefix2 = 0x45;
        public static byte Suffix = 0x56;


        public DeviceControl()
        {
            port = new SerialPort()
            {
                BaudRate = 9600,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                ReceivedBytesThreshold = 1
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

                port = new SerialPort()
                {
                    PortName = comPort,
                    BaudRate = 9600,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    ReceivedBytesThreshold = 1
                };

                try
                {
                    port.Open();
                    MessageBox.Show("Device Connected!");
                    statusLamp.Fill = (Brush)new BrushConverter().ConvertFromString("#2baf2b");
                    port.DataReceived -= Port_DataReceived;
                    port.DataReceived += Port_DataReceived;
                }
                catch (Exception)
                {
                    MessageBox.Show("Device Cannot be Connected!");
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
            if (port == null) { return; }

            try
            {
                if (port.IsOpen)
                {
                    port.Write(buf, 0, buf.Length);
                }
            }
            catch (Exception)
            {

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
            var data = IOtoData();
            if (port.IsOpen)
            {
                SendBytes(GetFrame(data));
            }
        }

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (port.IsOpen)
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