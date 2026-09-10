using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace LEDVision.Model
{
    public class Setting : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private int retestTimes = 2;

        public int RetestTimes
        {
            get
            {
                return retestTimes;
            }
            set
            {
                if (retestTimes != value)
                {
                    retestTimes = value;


                    NotifyPropertyChanged(nameof(RetestTimes));
                }
            }
        }


        private double waitRetest = 1000;

        public double WaitRetest
        {
            get
            {
                return waitRetest;
            }
            set
            {
                if (waitRetest != value)
                {
                    waitRetest = value;


                    NotifyPropertyChanged(nameof(WaitRetest));
                }
            }
        }

        private double delayUPDOWN = 1000;

        public double DelayUPDOWN
        {
            get
            {
                return delayUPDOWN;
            }
            set
            {
                if (delayUPDOWN != value)
                {
                    delayUPDOWN = value;


                    NotifyPropertyChanged(nameof(WaitRetest));
                }
            }
        }


        private int maxNum = 40000;

        public int MaxNum
        {
            get
            {
                return maxNum;
            }
            set
            {
                if (maxNum != value)
                {
                    maxNum = value;
               
                        ShouldWarning = curNum > maxNum;
                 
                        NotifyPropertyChanged(nameof(MaxNum));
                }
            }
        }


        private bool shouldWarning = false;

        public bool ShouldWarning
        {
            get
            {
                return shouldWarning;
            }
            set
            {
                if (shouldWarning != value)
                {
                    shouldWarning = value;
                    NotifyPropertyChanged(nameof(ShouldWarning));
                }
            }
        }

        private int curNum = 0;

        public int CurNum
        {
            get
            {
                return curNum;
            }
            set
            {
                if (curNum != value)
                {
                    curNum = value;
                    ShouldWarning = curNum >= maxNum && curNum!=0;

                    NotifyPropertyChanged(nameof(CurNum));
                }
            }
        }

        private string comPort = "";

        public string ComPort
        {
            get
            {
                return comPort;
            }
            set
            {
                if (comPort != value)
                {
                    comPort = value;
                    NotifyPropertyChanged(nameof(ComPort));
                }
            }
        }

        private string logDirectory = "";

        public string LogDirectory
        {
            get
            {
                return logDirectory;
            }
            set
            {
                if (logDirectory != value)
                {

                    logDirectory = value;
                    NotifyPropertyChanged(nameof(LogDirectory));
                }
            }
        }

        // Đường dẫn file model mở / lưu gần nhất → tự mở lại khi khởi động chương trình
        private string lastModelPath = "";
        public string LastModelPath
        {
            get
            {
                return lastModelPath;
            }
            set
            {
                if (lastModelPath != value)
                {
                    lastModelPath = value ?? "";
                    NotifyPropertyChanged(nameof(LastModelPath));
                }
            }
        }

        // Persist (chống nhấp nháy kết quả): giữ qua N khung hình liên tiếp mới đổi PASS/NG.
        private bool persistEnabled = false;
        public bool PersistEnabled
        {
            get
            {
                return persistEnabled;
            }
            set
            {
                if (persistEnabled != value)
                {
                    persistEnabled = value;
                    NotifyPropertyChanged(nameof(PersistEnabled));
                }
            }
        }

        private int persistFrames = 5;
        public int PersistFrames
        {
            get
            {
                return persistFrames;
            }
            set
            {
                if (persistFrames != value)
                {
                    persistFrames = value;
                    NotifyPropertyChanged(nameof(PersistFrames));
                }
            }
        }
    }
}