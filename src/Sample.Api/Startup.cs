namespace Sample.Api;

using Components.Consumers;
using Contracts;
using MassTransit;
using MassTransit.MongoDbIntegration.MessageData;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;


public class Startup
{
    static bool? _isRunningInContainer;

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    static bool IsRunningInContainer =>
        _isRunningInContainer ??= bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), out var inContainer) && inContainer;

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddHealthChecks();

        services.AddApplicationInsightsTelemetry();

        services.ConfigureTelemetryModule<DependencyTrackingTelemetryModule>((module, o) =>
        {
            module.IncludeDiagnosticSourceActivities.Add("MassTransit");
        });

        services.AddMassTransit(mt =>
        {
            mt.SetKebabCaseEndpointNameFormatter();

            mt.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(IsRunningInContainer ? "rabbitmq" : "localhost");

                MessageDataDefaults.ExtraTimeToLive = TimeSpan.FromDays(1);
                MessageDataDefaults.Threshold = 2000;
                MessageDataDefaults.AlwaysWriteToRepository = false;

                cfg.UseMessageData(new MongoDbMessageDataRepository(IsRunningInContainer ? "mongodb://mongo" : "mongodb://127.0.0.1", "attachments"));
            });
            
            mt.AddRequestClient<SubmitOrder>(new Uri($"queue:{KebabCaseEndpointNameFormatter.Instance.Consumer<SubmitOrderConsumer>()}"));

            mt.AddRequestClient<CheckOrder>();
        });

        services.Configure<HealthCheckPublisherOptions>(options =>
        {
            options.Delay = TimeSpan.FromSeconds(2);
            options.Predicate = check => check.Tags.Contains("ready");
        });

        services.AddOpenApiDocument(cfg => cfg.PostProcess = d => d.Info.Title = "Sample API Site");

        services.AddControllers();
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
            app.UseDeveloperExceptionPage();

        //            app.UseHttpsRedirection();

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
    }
}
