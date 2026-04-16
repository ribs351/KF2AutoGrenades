namespace KF2AutoGrenades.Configuration;

public class IdlerOptions
{
    public int LoopDelayMs { get; set; } = 1000;
    public int OcrIntervalMs { get; set; } = 2000;

    public double MatchConfidenceThreshold { get; set; } = 0.85;

    public int TargetWidth { get; set; } = 1920;
    public int TargetHeight { get; set; } = 1080;

    public string TemplatesPath { get; set; } = "Templates";
    public string TessdataPath { get; set; } = "tessdata";

    public string GrenadeKey { get; set; } = "G";
    public string PauseMenuKey { get; set; } = "Escape";

    public bool DebugMode { get; set; } = false;
    public bool SaveDebugFrames { get; set; } = false;
    public bool UseConsoleSkipTrader { get; set; } = true;
    public string ConsoleToggleKey { get; set; } = "~";
    public string ConsoleCommand { get; set; } = "RequestSkipTrader";
    // Typing/pacing configuration for sending the command
    public int ConsoleSendDelayMs { get; set; } = 250;
    public int ConsoleAttempts { get; set; } = 3;
    public int MonitorIndex { get; set; } = 0; // 0 = primary, 1 = second monitor, etc.
}