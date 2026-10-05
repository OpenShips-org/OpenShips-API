using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Microsoft.EntityFrameworkCore;
using OpenShipsAPI.Domain.Destination;
using OpenShipsAPI.Utility;
using OpenShipsAPI.Infrastructure.Database;
using OpenShipsAPI.Infrastructure.Ports;
using OpenShipsAPI.Infrastructure.Queues;
using OpenShipsAPI.Infrastructure.Streams.AisStream;
using OpenShipsAPI.Infrastructure.Streams.AisStream.Handlers;
using OpenShipsAPI.Infrastructure.Streams.Common;
using OpenShipsAPI.Infrastructure.Streams.Pelyr;
using OpenShipsAPI.Infrastructure.Streams.Pelyr.Handlers;

using OpenShipsAPI.Worker;

var builder = WebApplication.CreateBuilder(args);

var noAis = args.Contains("--no-ais");

// -----------------------------------------------------------------------------
// API
// -----------------------------------------------------------------------------

builder.Services
    .AddControllers();

builder.Services.AddOpenApi();

builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.DefaultApiVersion = new ApiVersion(1);
    options.ReportApiVersions = true;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Public", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });

    options.AddPolicy("Private", policy =>
    {
        policy
            .WithOrigins("https://my-website.example")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
    
    options.AddPolicy("Development", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

// -----------------------------------------------------------------------------
// Database
// -----------------------------------------------------------------------------

builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"));
});


// -----------------------------------------------------------------------------
// API/Core services
// -----------------------------------------------------------------------------

builder.Services.AddSingleton<AisMapper>();

if (args.Contains("--import-ports"))
{
    builder.Services.AddHttpClient<PortImporter>();
    builder.Services.AddHttpClient<PortAliasImporter>();
}

// -----------------------------------------------------------------------------
// AIS
// -----------------------------------------------------------------------------

if (!noAis)
{
    // Core
    builder.Services.AddSingleton<AisMessageDispatcher>();

    // Queues
    builder.Services.AddSingleton<DestinationQueue>();
    builder.Services.AddSingleton<AisEventQueue>();

    // AISStream handlers
    builder.Services.AddSingleton<IAisMessageHandler, APositionReport>();
    builder.Services.AddSingleton<IAisMessageHandler, AShipStaticData>();

    // Pelyr handlers
    builder.Services.AddSingleton<IAisMessageHandler, PositionHandler>();

    // Workers
    builder.Services.AddHostedService<AisStreamWorker>();
    builder.Services.AddHostedService<PelyrStreamWorker>();
    builder.Services.AddHostedService<MessageCountLogger>();
    builder.Services.AddHostedService<DestinationWorker>();
    builder.Services.AddHostedService<AisEventWorker>();

    builder.Services.AddSingleton<DestinationResolver>();
}

// -----------------------------------------------------------------------------
// Build application
// -----------------------------------------------------------------------------

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// -----------------------------------------------------------------------------
// Development
// -----------------------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "v1");
    });
    
    app.UseCors("Development");
}

// -----------------------------------------------------------------------------
// Port import
// -----------------------------------------------------------------------------

if (args.Contains("--import-ports"))
{
    using var scope = app.Services.CreateScope();

    var portImporter = scope.ServiceProvider
        .GetRequiredService<PortImporter>();

    var aliasImporter = scope.ServiceProvider
        .GetRequiredService<PortAliasImporter>();

    Console.WriteLine("================================");
    Console.WriteLine("       PORT IMPORT");
    Console.WriteLine("================================");
    Console.WriteLine();

    await portImporter.ImportAsync();

    Console.WriteLine();
    Console.WriteLine("================================");
    Console.WriteLine("       ALIAS IMPORT");
    Console.WriteLine("================================");
    Console.WriteLine();

    await aliasImporter.ImportAsync();

    Console.WriteLine();
    Console.WriteLine("================================");
    Console.WriteLine("       IMPORT SUCCESSFUL");
    Console.WriteLine("================================");
    Console.WriteLine();

    return;
}

// -----------------------------------------------------------------------------
// HTTP pipeline
// -----------------------------------------------------------------------------



if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.Run();