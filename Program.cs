using KF2AutoGrenades.Configuration;
using KF2AutoGrenades.Interfaces;
using KF2AutoGrenades.Services;
using KF2AutoGrenades.Services.Windows;
using KF2AutoGrenades.Services.Linux;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.Configure<IdlerOptions>(
            context.Configuration.GetSection("IdlerOptions"));

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<IScreenCaptureService, WindowsScreenCaptureService>();
            services.AddSingleton<IInputService, WindowsInputService>();
            services.AddSingleton<WindowsControlService>();
            services.AddSingleton<IControlService>(p => p.GetRequiredService<WindowsControlService>());
            services.AddHostedService(p => p.GetRequiredService<WindowsControlService>());
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            services.AddSingleton<IScreenCaptureService, LinuxScreenCaptureService>();
            services.AddSingleton<IInputService, LinuxInputService>();
            services.AddSingleton<LinuxControlService>();
            services.AddSingleton<IControlService>(p => p.GetRequiredService<LinuxControlService>());
            services.AddHostedService(p => p.GetRequiredService<LinuxControlService>());
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported OS");
        }

        services.AddSingleton<TemplateMatchingService>();
        services.AddSingleton<OcrService>();

        services.AddHostedService<Worker>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    })
    .Build()
    .Run();