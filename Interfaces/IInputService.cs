namespace KF2AutoGrenades.Interfaces;

public interface IInputService
{
    void KeyDown(string key);
    void KeyUp(string key);
    void KeyPress(string key, int delayMs = 50);
    Task<bool> ExecuteConsoleCommandAsync(string command, CancellationToken ct);
}