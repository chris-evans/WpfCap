using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
//using System.Windows.Threading;

namespace WpfCap
{
    public static class CameraDevices
    {
        public static readonly Guid SystemDeviceEnum = new Guid(0x62BE5D10, 0x60EB, 0x11D0, 0xBD, 0x3B, 0x00, 0xA0, 0xC9, 0x11, 0xCE, 0x86);
        public static readonly Guid VideoInputDevice = new Guid(0x860BB310, 0x5D01, 0x11D0, 0xBD, 0x3B, 0x00, 0xA0, 0xC9, 0x11, 0xCE, 0x86);
        
        public static IEnumerable<CapDevice> GetDevices()
        { return GetFilters().Select((x) => new CapDevice(x.MonikerString)); }

        public static FilterInfo GetFilter(string monikor)
        { return GetFilters().FirstOrDefault((x) => x.MonikerString == monikor); }

        public static IEnumerable<FilterInfo> GetFilters()
        {
            var filters = new List<FilterInfo>();
            var monikor = new IMoniker[1];
            var deviceEnum = Activator.CreateInstance(Type.GetTypeFromCLSID(SystemDeviceEnum)) as ICreateDevEnum;
            var videoDeviceEnum = VideoInputDevice;

            if (deviceEnum.CreateClassEnumerator(ref videoDeviceEnum, out var moniker, 0) == 0)
            {
                while (true)
                {
                    int r = moniker.Next(1, monikor, IntPtr.Zero);

                    if (r != 0 || monikor[0] == null)
                    { break; }

                    filters.Add(new FilterInfo(monikor[0]));
                    Marshal.ReleaseComObject(monikor[0]);
                    monikor[0] = null;
                }
            }

            return filters;
        }
    }

    public class CapDevice : IDisposable
    {
        #region Variables

        private readonly ManualResetEvent _running;
        private readonly string _name;
        private readonly string _monikor;

        private bool _disposed = false;

        private IMediaControl _control = null;
        private AMMediaType _mediaType = null;

        #endregion

        #region Constructor & destructor

        /// <summary>
        /// Initializes a specific capture device
        /// </summary>
        /// <param name="moniker">Moniker string that represents a specific device</param>
        /// <param name="desiredHeight">the desired height</param>
        /// <param name="desiredWidth">the desired width</param>
        public CapDevice(string moniker)
        {
            _running = new ManualResetEvent(true);
            _monikor = moniker;
            _name = CameraDevices.GetFilter(moniker)?.Name;
        }

        ~CapDevice()
        { Dispose(false); }

        public void Dispose(bool diposing)
        { Stop(); }

        public void Dispose()
        {
            GC.SuppressFinalize(true);
            Dispose(true);
            _disposed = true;
        }

        #endregion


        #region Methods

        private bool SelectWebcamResolution(IPin sourcePin, int desiredWidth, int desiredHeight)
        {
            var cfg = sourcePin as IAMStreamConfig;
            var result = cfg.GetNumberOfCapabilities(out var capabilitiesCount, out _);

            int acquiredHeight = 0;
            int acquiredWidth = 0;

            if (result == 0)
            {
                var caps = new VideoStreamConfigCaps();
                var gcHandle = GCHandle.Alloc(caps, GCHandleType.Pinned);

                try
                {
                    for (int i = 0; i != capabilitiesCount; ++i)
                    {
                        result = cfg.GetStreamCaps(i, out var capabilityInfo, gcHandle.AddrOfPinnedObject());

                        using (capabilityInfo)
                        {
                            var infoHeader = (VideoInfoHeader)Marshal.PtrToStructure(capabilityInfo.FormatPtr, typeof(VideoInfoHeader));

                            // if we get the resolution we expect then set
                            if (infoHeader.BmiHeader.Width == desiredWidth &&
                                infoHeader.BmiHeader.Height == desiredHeight &&
                                infoHeader.BmiHeader.BitCount != 0)
                            {
                                result = cfg.SetFormat(capabilityInfo);
                                break;
                            }
                        }
                    }
                }
                finally
                { gcHandle.Free(); }
            }

            return desiredWidth == acquiredWidth && desiredHeight == acquiredHeight;
        }

        /// <summary>
        /// Starts up thhe scanner.
        /// </summary>
        /// <returns></returns>
        public async Task<IObservable<InteropBitmap>> Start(int desiredWidth = 0, int desiredHeight = 0)
        {
            if (_disposed)
            { throw new ObjectDisposedException(nameof(CapDevice)); }

            if (!_running.Reset())
            { return null; }

            var startTask = new Task<IObservable<InteropBitmap>>(() =>
            {
                var graph = Activator.CreateInstance(Type.GetTypeFromCLSID(FilterGraph)) as IFilterGraph2;
                var sourceObject = FilterInfo.CreateFilter(_monikor);

                var outputPin = sourceObject.GetPin(PinCategory.Capture, 0);
                SelectWebcamResolution(outputPin, desiredWidth, desiredHeight);

                var grabber = Activator.CreateInstance(Type.GetTypeFromCLSID(SampleGrabber)) as ISampleGrabber;
                var grabberObject = grabber as IBaseFilter;

                if (graph == null)
                { return null; }

                graph.AddFilter(sourceObject, "source");
                graph.AddFilter(grabberObject, "grabber");
                _mediaType = new AMMediaType();
                _mediaType.MajorType = MediaTypes.Video;
                _mediaType.SubType = MediaSubTypes.RGB32;


                // Create new grabber
                FrameStream frameBuffer = null;
                int acquiredWidth = 0;
                int acquiredHeight = 0;
                if (grabber != null)
                {
                    grabber.SetMediaType(_mediaType);

                    var inputPin = grabberObject.GetPin(PinDirection.Input, 0);
                    if (graph.Connect(outputPin, inputPin) >= 0)
                    {
                        if (grabber.GetConnectedMediaType(_mediaType) == 0)
                        {
                            var header = (VideoInfoHeader)Marshal.PtrToStructure(_mediaType.FormatPtr, typeof(VideoInfoHeader));
                            acquiredWidth = header.BmiHeader.Width;
                            acquiredHeight = header.BmiHeader.Height;
                        }
                    }

                    graph.Render(grabberObject.GetPin(PinDirection.Output, 0));
                    grabber.SetBufferSamples(false);
                    grabber.SetOneShot(false);

                    frameBuffer = new FrameStream(acquiredWidth, acquiredHeight, PixelFormats.Bgr32);
                    var capGrabber = new CapGrabber(frameBuffer);
                    grabber.SetCallback(capGrabber, 1);
                }

                // Get the video window
                var wnd = (IVideoWindow)graph;
                wnd.put_AutoShow(false);

                // Create the control and run
                _control = (IMediaControl)graph;
                _control.Run();

                return frameBuffer?.Frame;
            });

            startTask.Start();
            return await startTask;
        }

        /// <summary>
        /// Stops grabbing images from the capture device
        /// </summary>
        public virtual void Stop()
        {
            Cleanup();
            _running.Set();
        }

        private void Cleanup()
        {
            if (_control != null)
            {
                _control.StopWhenReady();
                _mediaType?.Dispose();
            }

            _control = null;
            _mediaType = null;
        }

        #endregion

        #region Win32
        private static readonly Guid FilterGraph = new Guid(0xE436EBB3, 0x524F, 0x11CE, 0x9F, 0x53, 0x00, 0x20, 0xAF, 0x0B, 0xA7, 0x70);

        private static readonly Guid SampleGrabber = new Guid(0xC1F400A0, 0x3F08, 0x11D3, 0x9F, 0x0B, 0x00, 0x60, 0x08, 0x03, 0x9E, 0x37);

        public static readonly Guid SystemDeviceEnum = new Guid(0x62BE5D10, 0x60EB, 0x11D0, 0xBD, 0x3B, 0x00, 0xA0, 0xC9, 0x11, 0xCE, 0x86);

        public static readonly Guid VideoInputDevice = new Guid(0x860BB310, 0x5D01, 0x11D0, 0xBD, 0x3B, 0x00, 0xA0, 0xC9, 0x11, 0xCE, 0x86);

        public static readonly Guid Pin = new Guid(0x9b00f101, 0x1567, 0x11d1, 0xb3, 0xf1, 0x00, 0xaa, 0x00, 0x37, 0x61, 0xc5);

        [ComVisible(false)]
        internal class MediaTypes
        {
            public static readonly Guid Video = new Guid(0x73646976, 0x0000, 0x0010, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71);

            public static readonly Guid Interleaved = new Guid(0x73766169, 0x0000, 0x0010, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71);

            public static readonly Guid Audio = new Guid(0x73647561, 0x0000, 0x0010, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71);

            public static readonly Guid Text = new Guid(0x73747874, 0x0000, 0x0010, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71);

            public static readonly Guid Stream = new Guid(0xE436EB83, 0x524F, 0x11CE, 0x9F, 0x53, 0x00, 0x20, 0xAF, 0x0B, 0xA7, 0x70);
        }

        [ComVisible(false)]
        internal class MediaSubTypes
        {
            public static readonly Guid YUYV = new Guid(0x56595559, 0x0000, 0x0010, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71);

            public static readonly Guid IYUV = new Guid(0x56555949, 0x0000, 0x0010, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71);

            public static readonly Guid DVSD = new Guid(0x44535644, 0x0000, 0x0010, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71);

            public static readonly Guid RGB1 = new Guid(0xE436EB78, 0x524F, 0x11CE, 0x9F, 0x53, 0x00, 0x20, 0xAF, 0x0B, 0xA7, 0x70);

            public static readonly Guid RGB4 = new Guid(0xE436EB79, 0x524F, 0x11CE, 0x9F, 0x53, 0x00, 0x20, 0xAF, 0x0B, 0xA7, 0x70);

            public static readonly Guid RGB8 = new Guid(0xE436EB7A, 0x524F, 0x11CE, 0x9F, 0x53, 0x00, 0x20, 0xAF, 0x0B, 0xA7, 0x70);

            public static readonly Guid RGB565 = new Guid(0xE436EB7B, 0x524F, 0x11CE, 0x9F, 0x53, 0x00, 0x20, 0xAF, 0x0B, 0xA7, 0x70);

            public static readonly Guid RGB555 = new Guid(0xE436EB7C, 0x524F, 0x11CE, 0x9F, 0x53, 0x00, 0x20, 0xAF, 0x0B, 0xA7, 0x70);

            public static readonly Guid RGB24 = new Guid(0xE436Eb7D, 0x524F, 0x11CE, 0x9F, 0x53, 0x00, 0x20, 0xAF, 0x0B, 0xA7, 0x70);

            public static readonly Guid RGB32 = new Guid(0xE436EB7E, 0x524F, 0x11CE, 0x9F, 0x53, 0x00, 0x20, 0xAF, 0x0B, 0xA7, 0x70);

            public static readonly Guid Avi = new Guid(0xE436EB88, 0x524F, 0x11CE, 0x9F, 0x53, 0x00, 0x20, 0xAF, 0x0B, 0xA7, 0x70);

            public static readonly Guid Asf = new Guid(0x3DB80F90, 0x9412, 0x11D1, 0xAD, 0xED, 0x00, 0x00, 0xF8, 0x75, 0x4B, 0x99);
        }

        [ComVisible(false)]
        static public class PinCategory
        {
            public static readonly Guid Capture = new Guid(0xfb6c4281, 0x0353, 0x11d1, 0x90, 0x5f, 0x00, 0x00, 0xc0, 0xcc, 0x16, 0xba);

            public static readonly Guid Preview = new Guid(0xfb6c4282, 0x0353, 0x11d1, 0x90, 0x5f, 0x00, 0x00, 0xc0, 0xcc, 0x16, 0xba);

            public static readonly Guid AnalogVideoIn = new Guid(0xfb6c4283, 0x0353, 0x11d1, 0x90, 0x5f, 0x00, 0x00, 0xc0, 0xcc, 0x16, 0xba);

            public static readonly Guid VBI = new Guid(0xfb6c4284, 0x0353, 0x11d1, 0x90, 0x5f, 0x00, 0x00, 0xc0, 0xcc, 0x16, 0xba);

            public static readonly Guid VideoPort = new Guid(0xfb6c4285, 0x0353, 0x11d1, 0x90, 0x5f, 0x00, 0x00, 0xc0, 0xcc, 0x16, 0xba);

            public static readonly Guid NABTS = new Guid(0xfb6c4286, 0x0353, 0x11d1, 0x90, 0x5f, 0x00, 0x00, 0xc0, 0xcc, 0x16, 0xba);

            public static readonly Guid EDS = new Guid(0xfb6c4287, 0x0353, 0x11d1, 0x90, 0x5f, 0x00, 0x00, 0xc0, 0xcc, 0x16, 0xba);

            public static readonly Guid TeleText = new Guid(0xfb6c4288, 0x0353, 0x11d1, 0x90, 0x5f, 0x00, 0x00, 0xc0, 0xcc, 0x16, 0xba);

            public static readonly Guid CC = new Guid(0xfb6c4289, 0x0353, 0x11d1, 0x90, 0x5f, 0x00, 0x00, 0xc0, 0xcc, 0x16, 0xba);

            public static readonly Guid Still = new Guid(0xfb6c428a, 0x0353, 0x11d1, 0x90, 0x5f, 0x00, 0x00, 0xc0, 0xcc, 0x16, 0xba);

            public static readonly Guid TimeCode = new Guid(0xfb6c428b, 0x0353, 0x11d1, 0x90, 0x5f, 0x00, 0x00, 0xc0, 0xcc, 0x16, 0xba);

            public static readonly Guid VideoPortVBI = new Guid(0xfb6c428c, 0x0353, 0x11d1, 0x90, 0x5f, 0x00, 0x00, 0xc0, 0xcc, 0x16, 0xba);
        }

        #endregion

        #region IDisposable Members



        #endregion
    }
}