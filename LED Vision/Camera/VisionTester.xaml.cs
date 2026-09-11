using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LEDVision.Camera
{
    /// <summary>
    /// Interaction logic for VisionTester.xaml
    /// </summary>
    public partial class VisionTester : UserControl

    {

        private Model.SettingModel settingModel = new Model.SettingModel();

        public Model.SettingModel SettingModel
        {
            get
            {
                return settingModel;
            }
            set
            {
                if (settingModel != value)
                {
                    settingModel = value;
                   
                }
            }
        }

        private Model.Model programModel = new Model.Model();

        public Model.Model ProgramModel
        {
            get
            {
                return programModel;
            }
            set
            {
                if (programModel != value)
                {
                    programModel = value;
                    mainCanvas.Children.Clear();
                    foreach (var led in ProgramModel.Vision.AllLeds())
                    {
                        led.SetParentCanvas(mainCanvas);
                        mainCanvas.Children.Add(led.Roi);
                    }
                }
            }
        }

        public VisionTester()
        {
            InitializeComponent();
        }

        // Đặt kích thước bề mặt = khung hiển thị (giữ đúng tỉ lệ ảnh thô) giống VisionBuilder.
        // Canvas bind theo ActualWidth của mainGrid nên chỉ cần set mainGrid.
        public void SetSurfaceSize(double w, double h)
        {
            if (w <= 0 || h <= 0) return;
            this.Width = w;
            this.Height = h;
            mainGrid.Width = w;
            mainGrid.Height = h;
        }

        private void mainCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
        }
    }
}