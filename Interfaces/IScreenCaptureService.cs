using OpenCvSharp;

namespace KF2AutoGrenades.Interfaces;

public interface IScreenCaptureService
{
    Mat Capture();
    (int Width, int Height) GetScreenSize();
}
