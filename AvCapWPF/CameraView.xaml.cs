using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfCap;

namespace AvCapWPF
{
    /// <summary>
    /// Interaction logic for CameraView
    /// </summary>
    public partial class CameraView : UserControl
    {
        public CapDevice Device
        { get; private set; }

        public CameraView()
        {
            InitializeComponent();

            DeviceBox.ItemsSource = CameraDevices.GetDevices();
            DeviceBox.DisplayMemberPath = "Name";
            DeviceBox.SelectionChanged += async (o, e) =>
            {
                if (Device != null)
                { Device.Stop(); }

                Device = (CapDevice)DeviceBox.SelectedItem;
                var frames = await Device.Start();
                frames.ObserveOnDispatcher(DispatcherPriority.Render)
                    .Subscribe(DoIt);
            };

            DeviceBox.SelectedIndex = 0;
            CaptureButton.Click += CaptureButton_Click;
        }

        private void DoIt(InteropBitmap bs)
        {
            NextPicture.Source = bs;
            CapturedImage = bs;
        }

        private void CaptureButton_Click(object sender, RoutedEventArgs e)
        { Device.Stop(); }

        public BitmapSource CapturedImage { get; set; }
    }
}