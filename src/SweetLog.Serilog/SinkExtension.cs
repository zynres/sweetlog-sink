using Microsoft.Extensions.DependencyInjection;
using SweetLog.Serilog.Workers;
using SweetLog.Serilog.Buffer;
using Serilog.Configuration;
using Serilog;

namespace SweetLog.Serilog;

public static class SinkExtension
{
    public static void AddSweetLog(this IServiceCollection services, Action<SinkOptions>? configure = null)
    {
        var options = new SinkOptions();

        configure?.Invoke(options);

        services.AddSingleton<Sink>();
        services.AddSingleton(options);
        services.AddSingleton<BatchBuffer>();

        //services.AddHostedService<HeartbeatWorker>();
        services.AddHostedService<LogSenderWorker>();
    }

    public static LoggerConfiguration SweetLog(this LoggerSinkConfiguration sinkConfiguration, IServiceProvider services)
    {
        var sink = services.GetRequiredService<Sink>();

        return sinkConfiguration.Sink(sink);
    }
}
