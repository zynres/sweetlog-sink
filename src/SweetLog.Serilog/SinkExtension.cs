using Microsoft.Extensions.DependencyInjection;
using SweetLog.Serilog.Services.Encoders;
using SweetLog.Serilog.Services.Workers;
using SweetLog.Serilog.Services.Senders;
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

        services.AddSingleton(options);

        services.AddSingleton<LogEncoder>();
        services.AddSingleton<HeartbeatEncoder>();
        services.AddSingleton<ConfigurationEncoder>();
        
        services.AddSingleton<ConfigurationSender>();
        services.AddSingleton<HeartbeatSender>();
        services.AddSingleton<LogSender>();

        services.AddSingleton<Sink>();
        services.AddSingleton<BatchBuffer>();

        services.AddHostedService<LogSenderWorker>();
    }

    public static LoggerConfiguration SweetLog(this LoggerSinkConfiguration sinkConfiguration, IServiceProvider services)
    {
        var sink = services.GetRequiredService<Sink>();

        return sinkConfiguration.Sink(sink);
    }
}
