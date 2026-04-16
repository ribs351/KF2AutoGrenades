using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using KF2AutoGrenades.Configuration;
using KF2AutoGrenades.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KF2AutoGrenades.Services.Windows;

public class WindowsInputService : IInputService
{
    private readonly Dictionary<string, ushort> _keyMap;
    public bool blocking = false;
    private readonly int _screenWidth;
    private readonly int _screenHeight;
    private readonly ILogger<WindowsInputService> _logger;
    private readonly IdlerOptions _options;

    public WindowsInputService(
        ILogger<WindowsInputService> logger,
        IOptions<IdlerOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        _screenWidth = GetSystemMetrics(0);
        _screenHeight = GetSystemMetrics(1);

        _keyMap = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
        {
            ["G"] = 0x47,
            ["Escape"] = 0x1B,
            ["F"] = 0x46,
            ["Enter"] = 0x0D,
            ["Space"] = 0x20,
            ["Tab"] = 0x09
        };
    }

    public void KeyDown(string key)
    {
        if (!_keyMap.TryGetValue(key, out var keyCode))
            throw new InvalidOperationException($"Key '{key}' is not mapped in the key map.");

        SendKey(keyCode, false);
    }

    public void KeyUp(string key)
    {
        if (!_keyMap.TryGetValue(key, out var keyCode))
            throw new InvalidOperationException($"Key '{key}' is not mapped in the key map.");

        SendKey(keyCode, true);
    }

    public void KeyPress(string key, int delayMs = 50)
    {
        KeyDown(key);
        Thread.Sleep(delayMs);
        KeyUp(key);
    }

    /// <summary>
    /// Open the in-game console, type the command (typed keystrokes), press Enter and close the console.
    /// Returns true if the sequence was sent; callers must verify the effect via template matching.
    /// </summary>
    public async Task<bool> ExecuteConsoleCommandAsync(string command, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(command))
            return false;

        var fg = GetForegroundWindowInfo();
        if (fg.Hwnd == IntPtr.Zero)
        {
            _logger.LogWarning("No foreground window found.");
            return false;
        }

        try
        {
            EnsureForegroundAndFocus(fg.Hwnd);

            char toggleChar = !string.IsNullOrEmpty(_options.ConsoleToggleKey)
                ? _options.ConsoleToggleKey[0]
                : '`';

            ushort toggleVk = (ushort)(VkKeyScan(toggleChar) & 0xFF);

            SendVirtualKey(toggleVk);
            await Task.Delay(_options.ConsoleSendDelayMs, ct);

            foreach (char ch in command)
            {
                ct.ThrowIfCancellationRequested();

                SendVirtualKey((ushort)(VkKeyScan(ch) & 0xFF));
                await Task.Delay(_options.ConsoleSendDelayMs, ct);
            }

            KeyPress("Enter");

            await Task.Delay(_options.ConsoleSendDelayMs * 2, ct);

            SendVirtualKey(toggleVk);
            await Task.Delay(_options.ConsoleSendDelayMs, ct);

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ExecuteConsoleCommand failed: '{cmd}'", command);
            return false;
        }
    }

    private void EnsureForegroundAndFocus(IntPtr hwnd)
    {
        uint targetThreadId = GetWindowThreadProcessId(hwnd, out _);
        uint currentThreadId = GetCurrentThreadId();

        _logger.LogDebug("EnsureForegroundAndFocus: currentThread={cur}, targetThread={tgt}", currentThreadId, targetThreadId);

        bool attached = false;

        try
        {
            if (currentThreadId != targetThreadId)
            {
                attached = AttachThreadInput(currentThreadId, targetThreadId, true);
                if (!attached)
                {
                    _logger.LogWarning("AttachThreadInput failed (GetLastError={err})", Marshal.GetLastWin32Error());
                }
            }

            bool fg = SetForegroundWindow(hwnd);
            bool brought = BringWindowToTop(hwnd);
            IntPtr active = SetActiveWindow(hwnd);
            IntPtr focus = SetFocus(hwnd);

            _logger.LogDebug("SetForegroundWindow returned {fg}, BringWindowToTop {brought}, SetActiveWindow {active}, SetFocus {focus}",
                fg, brought, active != IntPtr.Zero, focus != IntPtr.Zero);

            Thread.Sleep(80);
        }
        finally
        {
            if (attached)
            {
                bool detached = AttachThreadInput(currentThreadId, targetThreadId, false);
                if (!detached)
                {
                    _logger.LogWarning("AttachThreadInput(detach) failed (GetLastError={err})", Marshal.GetLastWin32Error());
                }
            }
        }
    }


    private void SendVirtualKey(ushort vk)
    {
        var down = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0 } }
        };
        var up = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } }
        };
        SendInput(2, new[] { down, up }, Marshal.SizeOf<INPUT>());
    }

    private void SendKey(ushort keyCode, bool keyUp)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = keyCode,
                    dwFlags = keyUp ? KEYEVENTF_KEYUP : 0
                }
            }
        };

        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }
    private (nint Hwnd, string Title, string ProcessName, int ProcessId) GetForegroundWindowInfo()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return (0, string.Empty, string.Empty, 0);

        var sb = new StringBuilder(256);
        GetWindowText(hwnd, sb, sb.Capacity);

        GetWindowThreadProcessId(hwnd, out uint pid);
        string procName = string.Empty;
        try
        {
            var p = Process.GetProcessById((int)pid);
            procName = p.ProcessName;
        }
        catch { }

        return (hwnd, sb.ToString(), procName, (int)pid);
    }

    #region Win32

    private const int INPUT_KEYBOARD = 1;
    private const int KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    // AttachThreadInput/foreground helpers
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetActiveWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public int mouseData;
        public int dwFlags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public int dwFlags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    #endregion
}