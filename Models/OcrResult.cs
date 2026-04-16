namespace KF2AutoGrenades.Models;

public class OcrResult
{
    public bool IsBoss { get; set; }
    public int? Wave { get; set; }
    public bool IsEndless { get; set; }

    public string RawText { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"Boss: {IsBoss}, Wave: {Wave}, Endless: {IsEndless}, Raw: {RawText}";
    }
}