using System;
using SharpAvi;
using SharpAvi.Output;
using SharpAvi.Codecs;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenRecorder
{
    public class Recorder
    {
        private static int ScreenWidth = Screen.PrimaryScreen.Bounds.Width;
        private static int ScreenHeight = Screen.PrimaryScreen.Bounds.Height;
        public ManualResetEvent WaitHandler = new ManualResetEvent(false);

        public void Record()
        {
            var writer = new AviWriter("temp.avi")
            {
                FramesPerSecond = 30,
            };

            IAviVideoStream stream = writer.AddMotionJpegVideoStream(ScreenWidth, ScreenHeight);
            var frameData = new byte[stream.Width * stream.Height * 4];

            var stopwatch = new Stopwatch();
            var buffer = new byte[ScreenWidth * ScreenHeight * 4];

            var shotsTaken = 0;
            var timeTillNextFrame = TimeSpan.Zero;

            Task videoWriteTask = null;
            var isFirstFrame = true;
            stopwatch.Start();
            while (!WaitHandler.WaitOne(timeTillNextFrame))
            {
                GetScreenshot(buffer);
                if (!isFirstFrame)
                {
                    videoWriteTask.Wait();
                }

                videoWriteTask = stream.WriteFrameAsync(true, buffer, 0, buffer.Length);
                timeTillNextFrame = TimeSpan.FromSeconds(shotsTaken / (double)writer.FramesPerSecond - stopwatch.Elapsed.TotalSeconds);
                if (timeTillNextFrame < TimeSpan.Zero)
                    timeTillNextFrame = TimeSpan.Zero;

                isFirstFrame = false;
            }

            stopwatch.Stop();
            if (!isFirstFrame)
            {
                videoWriteTask.Wait();
            }

            writer.Close();
        }

        private void GetScreenshot(byte[] buffer)
        {
            using (var bitmap = new Bitmap(ScreenWidth, ScreenHeight))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(ScreenWidth, ScreenHeight));
                var bits = bitmap.LockBits(new Rectangle(0, 0, ScreenWidth, ScreenHeight), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
                Marshal.Copy(bits.Scan0, buffer, 0, buffer.Length);
                bitmap.UnlockBits(bits);
            }
        }

        public string SaveFile()
        {
            var newName = "Recording-" + DateTime.Now.Ticks + ".avi";
            File.Move("temp.avi", newName);
            return newName;
        }
    }
}
