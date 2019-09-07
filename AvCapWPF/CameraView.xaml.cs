using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

            DeviceBox.ItemsSource = CapDevice.GetDevices(this.Dispatcher);
            DeviceBox.DisplayMemberPath = "Name";
            DeviceBox.SelectionChanged += async (o, e) =>
            {
                if (Device != null)
                { Device.Stop(); }

                Device = (CapDevice)DeviceBox.SelectedItem;
                var frames = await Device.Start();
                frames.Throttle(TimeSpan.FromMilliseconds(33))
                    .ObserveOnDispatcher()
                    .Subscribe(DoIt);
            };

            DeviceBox.SelectedIndex = 0;
            CaptureButton.Click += CaptureButton_Click;
        }

        private void DoIt(InteropBitmap bs)
        {

            NextPicture.Source = bs;
            bs.Invalidate();
        }

        private void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            Device.Stop();

            // Store current image from the webcam
            //var bitmap = Player.CurrentBitmap;
            //if (bitmap == null) return;

            //Transform tr = new ScaleTransform(-1, 1);
            //var transformedBmp = new TransformedBitmap();
            //transformedBmp.BeginInit();
            //transformedBmp.Source = bitmap;
            //transformedBmp.Transform = tr;
            //transformedBmp.EndInit();
            //bitmap = transformedBmp;

            //CapturedImage = bitmap;
        }

        public BitmapSource CapturedImage { get; set; }
    }
}