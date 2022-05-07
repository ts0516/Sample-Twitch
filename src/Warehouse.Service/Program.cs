using System.Diagnostics;
using MassTransit;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Serilog;
using Serilog.Events;
using Warehouse.Components.Consumers;
using Warehouse.Components.StateMachines;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

var telemetryModule = new DependencyTrackingTelemetryModule();
telemetryModule.IncludeDiagnosticSourceActivities.Add("MassTransit");

var telemetryConfiguration = TelemetryConfiguration.CreateDefault();
telemetryConfiguration.InstrumentationKey = "6b4c6c82-3250-4170-97d3-245ee1449278";
telemetryConfiguration.TelemetryInitializers.Add(new HttpDependenciesParsingTelemetryInitializer());

var telemetryClient = new TelemetryClient(telemetryConfiguration);
telemetryModule.Initialize(telemetryConfiguration);

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config.AddEnvironmentVariables();
        if (args is { Length: > 0 })
            config.AddCommandLine(args);
    })
    .ConfigureServices((_, services) =>
    {
        services.AddMassTransit(mt =>
        {
            mt.SetKebabCaseEndpointNameFormatter();

            mt.AddConsumersFromNamespaceContaining<AllocateInventoryConsumer>();
            mt.AddSagaStateMachine<AllocationStateMachine, AllocationState>(typeof(AllocationStateMachineDefinition))
                .MongoDbRepository(r =>
                {
                    r.Connection = "mongodb://127.0.0.1";
                    r.DatabaseName = "allocations";
                });

            mt.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.UseMessageScheduler(new Uri("queue:quartz"));
                cfg.ConfigureEndpoints(ctx);
            });
        });
    })
    .ConfigureLogging((hostContext, logging) =>
    {
        logging.AddSerilog(dispose: true);
        logging.AddConfiguration(hostContext.Configuration.GetSection("Logging"));
    });

var isService = !(Debugger.IsAttached || args.Contains("--console"));

if (isService)
    await host.UseWindowsService().Build().RunAsync();
else
    await host.RunConsoleAsync();

telemetryClient.Flush();
telemetryModule.Dispose();

Log.CloseAndFlush();
