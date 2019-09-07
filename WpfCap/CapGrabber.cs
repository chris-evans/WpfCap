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

namespace WpfCap
{
    /// <summary>
    /// implementation allows for callback when samples are available.  Sample is immidiately forwarded
    /// to our framebuffer for appropriate handling and implementation.
    /// </summary>
    internal class CapGrabber : ISampleGrabberCB
    {
        private readonly FrameStream _frameBuffer;

        public CapGrabber(FrameStream frameBuffer)
        { _frameBuffer = frameBuffer; }

        public int SampleCB(double sampleTime, IntPtr sample)
        { return 0; }

        public int BufferCB(double sampleTime, IntPtr buffer, int bufferLen)
        {
            _frameBuffer?.NewFrameData(buffer, bufferLen);
            return 0;
        }
    }
}
