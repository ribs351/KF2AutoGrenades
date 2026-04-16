using KF2AutoGrenades.Interfaces;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KF2AutoGrenades.Services.Linux
{
    internal class LinuxControlService : IControlService, IHostedService
    {
        public bool IsRunning => throw new NotImplementedException();

        public bool IsExitRequested => throw new NotImplementedException();

        public Task StartAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
