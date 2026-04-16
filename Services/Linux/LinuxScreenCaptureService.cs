using KF2AutoGrenades.Interfaces;
using OpenCvSharp;

namespace KF2AutoGrenades.Services.Linux
{
    internal class LinuxScreenCaptureService : IScreenCaptureService
    {
        public Mat Capture()
        {
            throw new NotImplementedException();
        }

        public (int Width, int Height) GetScreenSize()
        {
            throw new NotImplementedException();
        }
    }
}
