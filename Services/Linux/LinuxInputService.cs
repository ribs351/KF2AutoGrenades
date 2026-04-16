using KF2AutoGrenades.Interfaces;

namespace KF2AutoGrenades.Services.Linux
{
    internal class LinuxInputService : IInputService
    {
        public Task<bool> ExecuteConsoleCommandAsync(string command, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public void KeyDown(string key)
        {
            throw new NotImplementedException();
        }

        public void KeyPress(string key, int delayMs = 50)
        {
            throw new NotImplementedException();
        }

        public void KeyUp(string key)
        {
            throw new NotImplementedException();
        }
    }
}
