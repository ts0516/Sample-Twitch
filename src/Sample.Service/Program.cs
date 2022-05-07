using System.Diagnostics;
using MassTransit;
using MassTransit.Courier.Contracts;
using MassTransit.MongoDbIntegration.MessageData;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Sample.Components.BatchConsumers;
using Sample.Components.Consumers;
using Sample.Components.CourierActivities;
using Sample.Components.StateMachines;
using Sample.Components.StateMachines.OrderStateMachineActivities;
using Serilog;
using Serilog.Events;
using Warehouse.Contracts;

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
        services.AddScoped<AcceptOrderActivity>();

        services.AddScoped<RoutingSlipBatchEventConsumer>();

        services.AddMassTransit(mt =>
        {
            mt.SetKebabCaseEndpointNameFormatter();

            mt.AddConsumersFromNamespaceContaining<SubmitOrderConsumer>();
            mt.AddActivitiesFromNamespaceContaining<AllocateInventoryActivity>();

            mt.AddSagaStateMachine<OrderStateMachine, OrderState>(typeof(OrderStateMachineDefinition))
                .MongoDbRepository(r =>
                {
                    r.Connection = "mongodb://127.0.0.1";
                    r.DatabaseName = "orders";
                });

            mt.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.UseMessageData(new MongoDbMessageDataRepository("mongodb://127.0.0.1", "attachments"));
                cfg.UseMessageScheduler(new Uri("queue:quartz"));

                cfg.ReceiveEndpoint(KebabCaseEndpointNameFormatter.Instance.Consumer<RoutingSlipBatchEventConsumer>(), e =>
                {
                    e.PrefetchCount = 20;

                    e.Batch<RoutingSlipCompleted>(b =>
                    {
                        b.MessageLimit = 10;
                        b.TimeLimit = TimeSpan.FromSeconds(5);

                        b.Consumer<RoutingSlipBatchEventConsumer, RoutingSlipCompleted>(ctx);
                    });
                });

                cfg.ConfigureEndpoints(ctx);
            });

            mt.AddRequestClient<AllocateInventory>();
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
