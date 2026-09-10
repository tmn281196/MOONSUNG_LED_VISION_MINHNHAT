using System;
using System.Reflection;
using System.Windows;

namespace LEDVision
{
    /// <summary>
    /// Popup giới thiệu (nút Info ở cuối sidebar trái): hiện 2 logo Moonsung / TNG và phiên bản app.
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            try
            {
                versionText.Text = "Version " + Assembly.GetExecutingAssembly().GetName().Version;
            }
            catch (Exception)
            {
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
