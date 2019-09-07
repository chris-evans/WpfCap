using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfCap
{
    public class FrameStream
    {
        private readonly Subject<InteropBitmap> _frame;
        private BufferEntry _bufferEntry;

        public IObservable<InteropBitmap> Frame
        { get => _frame; }

        public FrameStream(int width, int height, PixelFormat pixelFormat)
        {
            _frame = new Subject<InteropBitmap>();
            _bufferEntry = _bufferEntry = new BufferEntry(width, height, pixelFormat);
        }

        public void NewFrameData(IntPtr buffer, int bufferLen)
        { _frame.OnNext(_bufferEntry.Update(buffer, bufferLen)); }

        private class BufferEntry
        {
            private readonly int _width;
            private readonly int _height;
            private readonly PixelFormat _pixelFormat;

            private readonly InteropBitmap _bitmap;
            private readonly IntPtr _section;
            private readonly IntPtr _map;

            public BufferEntry(int width, int height, PixelFormat pixelFormat)
            {
                _pixelFormat = pixelFormat;
                _width = width;
                _height = height;

                // allocate our memory mapped file that will be used to update image content.
                var totalByteCount = (uint)(_width * _height * _pixelFormat.BitsPerPixel / 8);
                _section = CreateFileMapping(new IntPtr(-1), IntPtr.Zero, 0x04, 0, totalByteCount, null);
                _map = MapViewOfFile(_section, 0xF001F, 0, 0, totalByteCount);
            }

            public InteropBitmap Update(IntPtr buffer, int bufferLength)
            {
                // each time a new image comes in we will create a new bitmap based on the new provided data.  We will also freeze the bitmap so it can be assigned outside
                // of the UI thread.  This allows us to do almost all work off the UI thread, while the UI can simply update it's bound property to now include the new
                // bitmap.
                CopyMemory(_map, buffer, bufferLength);
                var bitmap = Imaging.CreateBitmapSourceFromMemorySection(_section, _width, _height, _pixelFormat, _width * _pixelFormat.BitsPerPixel / 8, 0) as InteropBitmap;
                bitmap.Freeze();
                return bitmap;
            }

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern IntPtr CreateFileMapping(IntPtr hFile, IntPtr lpFileMappingAttributes, uint flProtect, uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, uint dwNumberOfBytesToMap);

            [DllImport("Kernel32.dll", EntryPoint = "RtlMoveMemory")]
            private static extern void CopyMemory(IntPtr Destination, IntPtr Source, int Length);
        }
    }
}
