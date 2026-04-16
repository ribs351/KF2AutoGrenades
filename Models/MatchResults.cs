using OpenCvSharp;

namespace KF2AutoGrenades.Models;

public class MatchResult
{
    public bool Found { get; set; }
    public double Confidence { get; set; }

    public Point Location { get; set; }

    public Size Size { get; set; }

    public string TemplateName { get; set; } = string.Empty;

    public Point Center => new(
        Location.X + Size.Width / 2,
        Location.Y + Size.Height / 2
    );

    public Rect BoundingBox => new(Location, Size);

    public override string ToString() =>
        Found
            ? $"[{TemplateName}] Found at ({Location.X}, {Location.Y}) | " +
              $"Center: ({Center.X}, {Center.Y}) | " +
              $"Confidence: {Confidence:P1}"
            : $"[{TemplateName}] No match found";
    public static MatchResult NotFound(string templateName, double confidence) => new()
    {
        Found = false,
        TemplateName = templateName,
        Confidence = confidence
    };
}

