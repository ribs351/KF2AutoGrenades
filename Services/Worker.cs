using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KF2AutoGrenades.Configuration;
using KF2AutoGrenades.Interfaces;
using OpenCvSharp;

namespace KF2AutoGrenades.Services;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IScreenCaptureService _capture;
    private readonly TemplateMatchingService _matcher;
    private readonly OcrService _ocr;
    private readonly IInputService _input;
    private readonly IControlService _control;
    private readonly IdlerOptions _options;

    private readonly SemaphoreSlim _traderSemaphore = new(1, 1);

    private enum GameState
    {
        Unknown,
        Combat,
        Trader
    }

    private GameState _currentState = GameState.Unknown;
    private GameState _previousState = GameState.Unknown;

    private DateTime _lastGrenadeTime = DateTime.MinValue;
    private DateTime _lastOcrTime = DateTime.MinValue;

    private bool _traderHandled = false;

    // Suppress repeated false-positive trader detections for a short interval
    private DateTime _suppressTraderUntil = DateTime.MinValue;
    private readonly TimeSpan _falsePositiveHold = TimeSpan.FromSeconds(5);

    // Tunables (will move these to config later)
    private readonly TimeSpan _grenadeInterval = TimeSpan.FromMilliseconds(50);

    public Worker(
        ILogger<Worker> logger,
        IScreenCaptureService capture,
        TemplateMatchingService matcher,
        OcrService ocr,
        IInputService input,
        IControlService control,
        IOptions<IdlerOptions> options)
    {
        _logger = logger;
        _capture = capture;
        _matcher = matcher;
        _ocr = ocr;
        _input = input;
        _control = control;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started. Press F8 to toggle, F9 to exit.");

        while (!stoppingToken.IsCancellationRequested && !_control.IsExitRequested)
        {
            if (!_control.IsRunning)
            {
                ResetState();
                await Task.Delay(1000, stoppingToken);
                continue;
            }

            using var frame = _capture.Capture();

            if (frame.Empty())
            {
                _logger.LogWarning("Frame is empty!");
                return;
            }

            // === DETECTION ===
            var traderMatch = _matcher.Find("trader_clock", frame);
            var bossMatch = _matcher.Find("boss_badge", frame);

            _previousState = _currentState;

            // If we have a suppression window active, ignore trader detections until it expires.
            if (DateTime.UtcNow < _suppressTraderUntil)
            {
                _currentState = GameState.Combat;
                _logger.LogDebug("Trader detection suppressed until {time}. Treating as Combat.", _suppressTraderUntil);
            }
            else
            {
                _currentState = traderMatch.Found
                    ? GameState.Trader
                    : GameState.Combat;
            }

            if (_currentState != _previousState)
            {
                _logger.LogInformation("State changed: {state}", _currentState);

                if (_currentState == GameState.Combat)
                {
                    _traderHandled = false;
                }
            }

            // === STATE LOGIC ===
            switch (_currentState)
            {
                case GameState.Combat:
                    await HandleCombatAsync(stoppingToken);
                    break;

                case GameState.Trader:
                    await HandleTraderAsync(stoppingToken);
                    break;
            }

            // === OCR ===
            //if (DateTime.UtcNow - _lastOcrTime > TimeSpan.FromMilliseconds(_options.OcrIntervalMs))
            //{
            //    _lastOcrTime = DateTime.UtcNow;
            //    var frameCopy = frame.Clone();
            //    _ = Task.Run(() =>
            //    {
            //        RunOCR(frameCopy);
            //    });
            //}

            await Task.Delay(_options.LoopDelayMs, stoppingToken);
        }

        _logger.LogInformation("Worker stopped.");
    }
    private void RunOCR(Mat frameCopy)
    {
        try
        {
            //var skipMatch = _matcher.Find("trader_distance", frameCopy);
            //if (!skipMatch.Found)
            //{
            //    _input.KeyPress(_options.PauseMenuKey);
            //}
            var roi = new Rect(25, 132, 250, 120); // tune this
            var result = _ocr.ReadWaveInfo(frameCopy, roi);
            _logger.LogInformation("[OCR] {result}", result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR failed");
        }
        finally
        {
            frameCopy.Dispose();
        }
    }
    private Task HandleCombatAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (DateTime.UtcNow - _lastGrenadeTime < _grenadeInterval)
            return Task.CompletedTask;

        _input.KeyPress(_options.GrenadeKey);
        _lastGrenadeTime = DateTime.UtcNow;
        _logger.LogDebug("Grenade thrown");

        return Task.CompletedTask;
    }

    private async Task HandleTraderAsync(CancellationToken ct)
    {
        if (_traderHandled)
            return;

        // Prevent re-entry
        if (!await _traderSemaphore.WaitAsync(0, ct))
            return;

        try
        {
            _logger.LogDebug("Handling trader...");

            _input.KeyPress(_options.PauseMenuKey);
            await Task.Delay(2000, ct);

            using var menuFrame = _capture.Capture();
            if (menuFrame.Empty())
            {
                _logger.LogWarning("Menu frame empty during trader handling");
                return;
            }

            var skipMatch = _matcher.Find("skip_trader", menuFrame);

            if (!skipMatch.Found)
            {
                _logger.LogWarning(
                    "Skip Trader button not found — suppressing trader detection for {seconds}s",
                    _falsePositiveHold.TotalSeconds);

                _suppressTraderUntil = DateTime.UtcNow + _falsePositiveHold;

                _input.KeyPress(_options.PauseMenuKey);
                return;
            }

            _logger.LogDebug(
                "Skip Trader found at {x},{y} (Confidence: {c:P1})",
                skipMatch.Location.X,
                skipMatch.Location.Y,
                skipMatch.Confidence);

            int maxAttempts = _options.ConsoleAttempts > 0 ? _options.ConsoleAttempts : 3;
            bool success = false;

            for (int attempt = 1; attempt <= maxAttempts && !success; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    await Task.Delay(300, ct);

                    if (_options.UseConsoleSkipTrader)
                    {
                        _logger.LogDebug(
                            "Sending console command '{cmd}' (attempt {attempt})",
                            _options.ConsoleCommand,
                            attempt);

                        bool sent = await _input.ExecuteConsoleCommandAsync(_options.ConsoleCommand, ct);

                        if (!sent)
                        {
                            _logger.LogWarning("Console send failed (attempt {attempt})", attempt);
                        }
                        else
                        {
                            await Task.Delay(500, ct);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Console command failed (attempt {attempt})", attempt);
                    await Task.Delay(150, ct);
                    continue;
                }

                await Task.Delay(2000, ct);

                using var verifyFrame = _capture.Capture();
                if (verifyFrame.Empty())
                {
                    _logger.LogWarning("Verify frame empty");
                    continue;
                }

                var verifyMatch = _matcher.Find("skip_trader", verifyFrame);

                if (!verifyMatch.Found)
                {
                    success = true;
                    _logger.LogDebug("Skip Trader cleared (attempt {attempt})", attempt);
                    _traderHandled = true;
                    break;
                }

                _logger.LogDebug("Skip Trader still present (attempt {attempt})", attempt);
                await Task.Delay(1500, ct);
            }

            if (!success)
            {
                _logger.LogError("Failed to clear Skip Trader after {maxAttempts}", maxAttempts);
                _input.KeyPress(_options.PauseMenuKey);
            }
        }
        finally
        {
            _traderSemaphore.Release();
        }
    }

    private void ResetState()
    {
        _traderHandled = false;
    }
}