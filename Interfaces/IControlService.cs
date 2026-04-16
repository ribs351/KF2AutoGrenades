namespace KF2AutoGrenades.Interfaces;

public interface IControlService
{
    bool IsRunning { get; }
    bool IsExitRequested { get; }
}