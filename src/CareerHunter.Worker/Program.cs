using CareerHunter.Application;
using CareerHunter.Infrastructure;
using CareerHunter.Infrastructure.Persistence;
using CareerHunter.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var options = WorkerOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(options);
builder.Services.AddHostedService<JobPipelineWorker>();

var host = builder.Build();

await using (var scope = host.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

await host.RunAsync();
