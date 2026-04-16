using KF2AutoGrenades.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace KF2AutoGrenades.Services.Windows;

public class WindowsControlService : IControlService, IHostedService, IDisposable
{
    private const int HOTKEY_ID_TOGGLE = 1;
    private const int HOTKEY_ID_EXIT = 2;

    private const int WM_HOTKEY = 0x0312;

    private Thread _messageLoopThread;
    private bool _running = true;

    private volatile bool _isRunning = false;
    private volatile bool _exitRequested = false;

    public bool IsRunning => _isRunning;
    public bool IsExitRequested => _exitRequested;

    private readonly ILogger<WindowsControlService> _logger;


    public WindowsControlService(ILogger<WindowsControlService> logger)
    {
        _logger = logger;
        _messageLoopThread = new Thread(MessageLoop)
        {
            IsBackground = true
        };
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _messageLoopThread = new Thread(MessageLoop)
        {
            IsBackground = true
        };

        _messageLoopThread.Start();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _running = false;
        return Task.CompletedTask;
    }

    private void MessageLoop()
    {
        // Register hotkeys:
        // F8 = toggle
        // F9 = exit

        RegisterHotKey(IntPtr.Zero, HOTKEY_ID_TOGGLE, 0, (uint)VirtualKey.F8);
        RegisterHotKey(IntPtr.Zero, HOTKEY_ID_EXIT, 0, (uint)VirtualKey.F9);

        try
        {
            while (_running)
            {
                if (GetMessage(out MSG msg, IntPtr.Zero, 0, 0))
                {
                    if (msg.message == WM_HOTKEY)
                    {
                        HandleHotkey(msg.wParam.ToInt32());
                    }
                }
            }
        }
        finally
        {
            UnregisterHotKey(IntPtr.Zero, HOTKEY_ID_TOGGLE);
            UnregisterHotKey(IntPtr.Zero, HOTKEY_ID_EXIT);
        }
    }

    private void HandleHotkey(int id)
    {
        switch (id)
        {
            case HOTKEY_ID_TOGGLE:
                _isRunning = !_isRunning;
                if (_isRunning)
                _logger.LogInformation("Automation STARTED");
                else
                    _logger.LogInformation("Automation PAUSED");
                break;

            case HOTKEY_ID_EXIT:
                _exitRequested = true;
                _running = false;
                _logger.LogInformation("EXIT requested");
                Environment.Exit(0);
                break;
        }
    }

    public void Dispose()
    {
        _running = false;
    }

    #region Win32 - hotkey/message helpers

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(
        IntPtr hWnd,
        int id,
        uint fsModifiers,
        uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(
        IntPtr hWnd,
        int id);

    [DllImport("user32.dll")]
    private static extern bool GetMessage(
        out MSG lpMsg,
        IntPtr hWnd,
        uint wMsgFilterMin,
        uint wMsgFilterMax);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    private enum VirtualKey : uint
    {
        F8 = 0x77,
        F9 = 0x78
    }

    #endregion
}