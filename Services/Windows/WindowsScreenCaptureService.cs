using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenCvSharp;
using KF2AutoGrenades.Interfaces;
using KF2AutoGrenades.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KF2AutoGrenades.Services.Windows;

public class WindowsScreenCaptureService : IScreenCaptureService, IDisposable
{
    private IntPtr _desktopDC;
    private IntPtr _memoryDC;
    private IntPtr _bitmap;
    private IntPtr _oldBitmap;

    private readonly int _width;
    private readonly int _height;
    private readonly int _originX;  // virtual desktop X of the chosen monitor
    private readonly int _originY;  // virtual desktop Y of the chosen monitor

    private byte[] _buffer;
    private GCHandle _bufferHandle;
    private Mat _mat;
    private BITMAPINFO _bmi;

    private readonly ILogger<WindowsScreenCaptureService> _logger;

    public WindowsScreenCaptureService(
        ILogger<WindowsScreenCaptureService> logger,
        IOptions<IdlerOptions> options)
    {
        _logger = logger;

        var monitors = EnumerateMonitors();

        // Sort left-to-right by X so index 0 = leftmost monitor
        monitors.Sort((a, b) => a.rcMonitor.left.CompareTo(b.rcMonitor.left));

        int idx = options.Value.MonitorIndex;
        if (idx < 0 || idx >= monitors.Count)
            throw new ArgumentOutOfRangeException(
                $"MonitorIndex {idx} is invalid — {monitors.Count} monitor(s) detected.");

        var chosen = monitors[idx];
        _originX = chosen.rcMonitor.left;
        _originY = chosen.rcMonitor.top;
        _width = chosen.rcMonitor.right - chosen.rcMonitor.left;
        _height = chosen.rcMonitor.bottom - chosen.rcMonitor.top;
        _logger.LogInformation(
            "Platform: Windows; Capturing monitor [{idx}]: origin=({x},{y}) size={w}x{h}",
            idx, _originX, _originY, _width, _height);

        // DC setup — GetDesktopWindow gives the full virtual desktop, which covers all monitors
        _desktopDC = GetWindowDC(GetDesktopWindow());
        _memoryDC = CreateCompatibleDC(_desktopDC);
        _bitmap = CreateCompatibleBitmap(_desktopDC, _width, _height);
        _oldBitmap = SelectObject(_memoryDC, _bitmap);

        _buffer = new byte[_width * _height * 4];
        _bufferHandle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);

        _bmi = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = _width,
                biHeight = -_height, // negative = top-down, no flip needed
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0 // BI_RGB
            }
        };

        _mat = Mat.FromPixelData(_height, _width, MatType.CV_8UC4, _bufferHandle.AddrOfPinnedObject());
    }

    public Mat Capture()
    {
        // xSrc/ySrc are the virtual desktop coords of the monitor's top-left corner
        BitBlt(_memoryDC, 0, 0, _width, _height, _desktopDC, _originX, _originY, SRCCOPY);

        GetDIBits(
            _memoryDC,
            _bitmap,
            0,
            (uint)_height,
            _bufferHandle.AddrOfPinnedObject(),
            ref _bmi,
            DIB_RGB_COLORS);

        return _mat.Clone();
    }

    public (int Width, int Height) GetScreenSize() => (_width, _height);

    // -------------------------------------------------------------------------

    private static List<MONITORINFO> EnumerateMonitors()
    {
        var monitors = new List<MONITORINFO>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnumCallback, IntPtr.Zero);
        return monitors;

        bool MonitorEnumCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(hMonitor, ref info))
                monitors.Add(info);
            return true;
        }
    }

    public void Dispose()
    {
        if (_oldBitmap != IntPtr.Zero) SelectObject(_memoryDC, _oldBitmap);
        if (_bitmap != IntPtr.Zero) DeleteObject(_bitmap);
        if (_memoryDC != IntPtr.Zero) DeleteDC(_memoryDC);
        if (_desktopDC != IntPtr.Zero) ReleaseDC(IntPtr.Zero, _desktopDC);
        if (_bufferHandle.IsAllocated) _bufferHandle.Free();
        _mat?.Dispose();
    }

    #region Win32

    private const int SRCCOPY = 0x00CC0020;
    private const uint DIB_RGB_COLORS = 0;

    [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);

    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(
        IntPtr hdcDest, int xDest, int yDest, int width, int height,
        IntPtr hdcSrc, int xSrc, int ySrc, int rop);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(
        IntPtr hdc, IntPtr hbmp, uint start, uint lines,
        IntPtr buffer, ref BITMAPINFO bmi, uint usage);

    private delegate bool MonitorEnumProc(
        IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor; // full monitor bounds in virtual desktop coords
        public RECT rcWork;    // work area (excludes taskbar)
        public uint dwFlags;   // MONITORINFOF_PRIMARY = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    #endregion
}