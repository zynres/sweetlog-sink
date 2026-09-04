using Microsoft.Extensions.DependencyInjection;
using SweetLog.Serilog.Workers;
using SweetLog.Serilog.Buffer;

namespace SweetLog.Serilog;

public static class SinkExtension
{
    public static void AddSweetLog(this IServiceCollection services)
    {
        services.AddHostedService<LogSenderWorker>();

        services.AddSingleton<BatchBuffer>();
        services.AddSingleton<Sink>();
    }
}
