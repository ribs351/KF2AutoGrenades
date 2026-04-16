using KF2AutoGrenades.Configuration;
using KF2AutoGrenades.Models;
using Microsoft.Extensions.Options;
using OpenCvSharp;
using System.Text.RegularExpressions;
using Tesseract;

namespace KF2AutoGrenades.Services;

public class OcrService : IDisposable
{
    private readonly IdlerOptions _options;
    private readonly TesseractEngine _engine;
    private readonly Mat _gray = new();
    private readonly Mat _binary = new();
    private readonly Mat _resized = new();

    // Regex for wave parsing
    private static readonly Regex WaveRegex = new(@"(\d+)\/(\d+|∞)", RegexOptions.Compiled);

    public OcrService(IOptions<IdlerOptions> options)
    {
        _options = options.Value;

        _engine = new TesseractEngine(
            _options.TessdataPath,
            "eng",
            EngineMode.Default);

        _engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789/∞");
    }

    public OcrResult ReadWaveInfo(Mat frame, OpenCvSharp.Rect roi)
    {
        using var region = new Mat(frame, roi);

        // Convert to grayscale
        Cv2.CvtColor(region, _gray, ColorConversionCodes.BGRA2GRAY);

        bool needsResize =
            frame.Width != _options.TargetWidth ||
            frame.Height != _options.TargetHeight;

        Mat workingMat = _gray;

        if (needsResize)
        {
            // Scale relative to target resolution
            double scaleX = (double)_options.TargetWidth / frame.Width;
            double scaleY = (double)_options.TargetHeight / frame.Height;

            Cv2.Resize(_gray, _resized,
                new Size(),
                scaleX,
                scaleY,
                InterpolationFlags.Lanczos4);

            // Blur ONLY if resized (helps OCR after interpolation)
            Cv2.GaussianBlur(_resized, _resized, new Size(3, 3), 0);

            workingMat = _resized;
        }

        // Threshold for OCR clarity
        Cv2.Threshold(workingMat, _binary, 150, 255, ThresholdTypes.Binary);

        using var pix = Pix.LoadFromMemory(_binary.ToBytes(".png"));
        using var page = _engine.Process(pix);

        var text = page.GetText() ?? string.Empty;
        text = NormalizeText(text);

        return Parse(text);
    }

    private static string NormalizeText(string text)
    {
        text = text.ToUpper();

        // Normalize common OCR mistakes for infinity
        text = text.Replace("OO", "∞")
                   .Replace("00", "∞")
                   .Replace("O0", "∞")
                   .Replace("INF", "∞");

        return text;
    }

    private static OcrResult Parse(string text)
    {
        var result = new OcrResult
        {
            RawText = text
        };

        if (text.Contains("BOSS"))
        {
            result.IsBoss = true;
        }

        var match = WaveRegex.Match(text);
        if (match.Success)
        {
            if (int.TryParse(match.Groups[1].Value, out int wave))
            {
                result.Wave = wave;
            }

            var second = match.Groups[2].Value;
            if (second == "∞")
            {
                result.IsEndless = true;
            }
        }

        return result;
    }

    public void Dispose()
    {
        _engine.Dispose();

        _gray.Dispose();
        _binary.Dispose();
        _resized.Dispose();
    }
}