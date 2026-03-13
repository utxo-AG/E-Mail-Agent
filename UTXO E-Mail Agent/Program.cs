using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UTXO_E_Mail_Agent.Api;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent.Services;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent;

public class Program
{
    // Version information - update this with each release
    private const string Version = "1.5.0";
    private const string BuildDate = "2026-03-12";

    public static async Task Main(string[] args)
    {
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine("  UTXO E-Mail Agent");
        Console.WriteLine($"  Version: {Version} (Build: {BuildDate})");
        Console.WriteLine("═══════════════════════════════════════════════════");

        var builder = WebApplication.CreateBuilder(args);

        // Configuration
        var configuration = builder.Configuration;
        var pollingIntervalSeconds = int.Parse(configuration["AppSettings:PollingIntervalSeconds"] ?? "60");
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        // Initialize logger with database connection
        Logger.Initialize(connectionString);

        // Add services
        builder.Services.AddDbContext<DefaultdbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                mysqlOptions => mysqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

        // Add CORS for Admintool access
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll",
                policyBuilder => policyBuilder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader());
        });

        // Add background service for email polling
        builder.Services.AddSingleton<IConfiguration>(configuration);
        builder.Services.AddHostedService<EmailPollingService>();

        // Add background task queue for fire-and-forget email processing
        builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
        builder.Services.AddHostedService<EmailProcessingWorker>();

        // Register HttpClient for API calls
        builder.Services.AddHttpClient();

        // Add Swagger/OpenAPI support
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Register server in database on startup
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DefaultdbContext>();
            await ServerRegistrationService.RegisterServerAsync(db);
        }

        // Deregister server on shutdown
        app.Lifetime.ApplicationStopping.Register(() =>
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DefaultdbContext>();
            ServerRegistrationService.DeregisterServerAsync(db).GetAwaiter().GetResult();
        });

        // Configure middleware
        app.UseCors("AllowAll");

        // Enable Swagger middleware
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "UTXO E-Mail Agent API v1");
            options.RoutePrefix = "swagger"; // Swagger will be available at /swagger
        });

        // Map API endpoints (from separate files in Api folder)
        app.MapProcessTextEndpoints();
        app.MapProcessEmailEndpoints();
        app.MapSendEmailEndpoints();
        app.MapSearchConversationsEndpoints();
        app.MapHealthEndpoints(Version);

        Console.WriteLine($"Polling interval: {pollingIntervalSeconds} seconds");
        Console.WriteLine("API running on: http://localhost:5051");
        Console.WriteLine("Endpoints:");
        Console.WriteLine("  POST /api/processtext - Process text with AI");
        Console.WriteLine("  POST /api/processemail - Queue email for AI processing");
        Console.WriteLine("  POST /api/send_email - Send email via agent's provider");
        Console.WriteLine("  POST /api/search_conversations - Search previous conversations");
        Console.WriteLine("  POST /api/get_attachment - Download conversation attachment");
        Console.WriteLine("  GET /api/health - Health check");
        Console.WriteLine("");
        Console.WriteLine("Swagger UI available at: http://localhost:5051/swagger");

        // Run the web application
        await app.RunAsync("http://0.0.0.0:5051");
    }
}
