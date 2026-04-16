using OpenCvSharp;
using KF2AutoGrenades.Models;
using KF2AutoGrenades.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace KF2AutoGrenades.Services;

public class TemplateMatchingService : IDisposable
{
    private readonly IdlerOptions _options;

    private readonly Dictionary<string, Mat> _templatesGray = new();

    private readonly Mat _resized = new();
    private readonly Mat _gray = new();
    private readonly Mat _result = new();

    private readonly Size _targetSize;
    private readonly ILogger<TemplateMatchingService> _logger;

    public TemplateMatchingService(IOptions<IdlerOptions> options, ILogger<TemplateMatchingService> logger)
    {
        _logger = logger;
        _options = options.Value;
        _targetSize = new Size(_options.TargetWidth, _options.TargetHeight);

        LoadTemplates();
    }

    private void LoadTemplates()
    {
        var files = Directory.GetFiles(_options.TemplatesPath, "*.png");

        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);

            var img = Cv2.ImRead(file, ImreadModes.Color);
            var gray = new Mat();

            Cv2.CvtColor(img, gray, ColorConversionCodes.BGR2GRAY);
            _logger.LogDebug("Loaded {Count} templates from {Path}", _templatesGray.Count, _options.TemplatesPath);
            _templatesGray[name] = gray;

            img.Dispose();
        }
    }

    public MatchResult Find(string templateName, Mat frame, Rect? roi = null)
    {
        if (!_templatesGray.TryGetValue(templateName, out var template))
        {
            throw new ArgumentException($"Template '{templateName}' not found.");
        }

        _logger.LogDebug($"Attempting to find {templateName}");

        Mat working = frame;
        bool resized = false;

        if (frame.Size() != _targetSize)
        {
            Cv2.Resize(frame, _resized, _targetSize);
            working = _resized;
            resized = true;
        }

        Cv2.CvtColor(working, _gray, ColorConversionCodes.BGRA2GRAY);

        if (resized)
        {
            Cv2.GaussianBlur(_gray, _gray, new Size(3, 3), 0);
        }

        Mat searchRegion = _gray;
        Point offset = new(0, 0);

        if (roi.HasValue)
        {
            searchRegion = new Mat(_gray, roi.Value);
            offset = roi.Value.Location;
        }

        Cv2.MatchTemplate(searchRegion, template, _result, TemplateMatchModes.CCoeffNormed);

        Cv2.MinMaxLoc(_result, out _, out double maxVal, out _, out Point maxLoc);

        if (maxVal < _options.MatchConfidenceThreshold)
        {
            return MatchResult.NotFound(templateName, maxVal);
        }

        // Adjust location if ROI was used
        var finalLocation = new Point(
            maxLoc.X + offset.X,
            maxLoc.Y + offset.Y
        );

        return new MatchResult
        {
            Found = true,
            Confidence = maxVal,
            Location = finalLocation,
            Size = template.Size(),
            TemplateName = templateName
        };
    }

    public void Dispose()
    {
        foreach (var t in _templatesGray.Values)
            t.Dispose();

        _resized.Dispose();
        _gray.Dispose();
        _result.Dispose();
    }
}