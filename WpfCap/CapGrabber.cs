///////////////////////////////////////////////////////////////////////////////
// CapGrabber
//
// This software is released into the public domain.  You are free to use it
// in any way you like, except that you may not sell this source code.
//
// This software is provided "as is" with no expressed or implied warranty.
// I accept no liability for any damage or loss of business that this software
// may cause.
// 
// This source code is originally written by Tamir Khason (see http://blogs.microsoft.co.il/blogs/tamir
// or http://www.codeplex.com/wpfcap).
// 
// Modifications are made by Geert van Horrik (CatenaLogic, see http://blog.catenalogic.com) 
// 
///////////////////////////////////////////////////////////////////////////////

using System;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Reactive;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;

namespace WpfCap
{
    internal class CapGrabber : ISampleGrabberCB, INotifyPropertyChanged
    {
        #region Win32 imports
        [DllImport("Kernel32.dll", EntryPoint = "RtlMoveMemory")]
        private static extern void CopyMemory(IntPtr Destination, IntPtr Source, int Length);
        #endregion

        #region Variables
        private int _height = default(int);
        private int _width = default(int);
        private FrameBuffer _frameBuffer;
        #endregion 

        #region Constructor & destructor
        public CapGrabber()
        { }
        #endregion

        #region Events
        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler NewFrameArrived;
        #endregion

        #region Properties

        public IntPtr Map { get; set; }

        public IObservable<InteropBitmap> Frame
        { get => _frameBuffer?.Frame; }

        public int Width
        {
            get { return _width; }
            set
            {
                _width = value;
                OnPropertyChanged("Width");
            }
        }

        public int Height
        {
            get { return _height; }
            set
            {
                _height = value;
                OnPropertyChanged("Height");
            }
        }

        #endregion

        #region Methods
        public int SampleCB(double sampleTime, IntPtr sample)
        {
            return 0;
        }

        public int BufferCB(double sampleTime, IntPtr buffer, int bufferLen)
        {
            _frameBuffer?.NewFrameData(buffer, bufferLen);

            if (Map != IntPtr.Zero)
            {
                CopyMemory(Map, buffer, bufferLen);
                OnNewFrameArrived();
            }

            return 0;
        }

        void OnNewFrameArrived()
        { NewFrameArrived?.Invoke(this, null); }

        void OnPropertyChanged(string name)
        {
            _frameBuffer = new FrameBuffer(Width, Height, PixelFormats.Bgr32);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion
    }
}
