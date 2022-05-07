using MassTransit;
using MassTransit.MongoDbIntegration.MessageData;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sample.Components.Consumers;
using Sample.Contracts;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

Log.Information("Starting web host");

try
{
    var builder = WebApplication.CreateBuilder(args);

    var isRunningInContainer = bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), out var inContainer) && inContainer;

    builder.Services.AddHealthChecks();

    builder.Services.AddApplicationInsightsTelemetry();

    builder.Services.ConfigureTelemetryModule<DependencyTrackingTelemetryModule>((module, o) =>
    {
        module.IncludeDiagnosticSourceActivities.Add("MassTransit");
    });

    builder.Services.AddMassTransit(mt =>
    {
        mt.SetKebabCaseEndpointNameFormatter();

        mt.UsingRabbitMq((_, cfg) =>
        {
            cfg.Host(isRunningInContainer ? "rabbitmq" : "localhost");

            MessageDataDefaults.ExtraTimeToLive = TimeSpan.FromDays(1);
            MessageDataDefaults.Threshold = 2000;
            MessageDataDefaults.AlwaysWriteToRepository = false;

            cfg.UseMessageData(new MongoDbMessageDataRepository(isRunningInContainer ? "mongodb://mongo" : "mongodb://127.0.0.1", "attachments"));
        });

        mt.AddRequestClient<SubmitOrder>(new Uri($"queue:{KebabCaseEndpointNameFormatter.Instance.Consumer<SubmitOrderConsumer>()}"));

        mt.AddRequestClient<CheckOrder>();
    });

    builder.Services.Configure<HealthCheckPublisherOptions>(options =>
    {
        options.Delay = TimeSpan.FromSeconds(2);
        options.Predicate = check => check.Tags.Contains("ready");
    });

    builder.Services.AddOpenApiDocument(cfg => cfg.PostProcess = d => d.Info.Title = "Sample API Site");

    builder.Services.AddControllers();

    builder.Host.UseSerilog();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
        app.UseDeveloperExceptionPage();

    // app.UseHttpsRedirection();

    app.UseOpenApi(); // serve OpenAPI/Swagger documents
    app.UseSwaggerUi3(); // serve Swagger UI

    app.UseRouting();

    app.UseAuthorization();

    app.UseEndpoints(endpoints =>
    {
        endpoints.MapControllers();

        // The readiness check uses all registered checks with the 'ready' tag.
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        });

        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            // Exclude all checks and return a 200-Ok.
            Predicate = _ => false
        });
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
