using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent.Factory;
using UTXO_E_Mail_Agent.Models;
using UTXO_E_Mail_Agent.Services;
using UTXO_E_Mail_Agent_Shared.Models;

// To update models from database:
// dotnet ef dbcontext scaffold "server=YOUR_SERVER;port=YOUR_PORT;user=YOUR_USER;password=YOUR_PASSWORD;database=YOUR_DB" Pomelo.EntityFrameworkCore.MySql -o Models --project "../UTXO E-Mail Agent Shared" --force --no-onconfiguring

// The --no-onconfiguring flag prevents hardcoding credentials in DefaultdbContext.cs

namespace UTXO_E_Mail_Agent;

public class Program
{
    // Version information - update this with each release
    private const string Version = "1.4.0";
    private const string BuildDate = "2026-02-10";

    private static IConfiguration _configuration = null!;
    private static int _pollingIntervalSeconds;
    private static string _connectionString = null!;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine("  UTXO E-Mail Agent");
        Console.WriteLine($"  Version: {Version} (Build: {BuildDate})");
        Console.WriteLine("═══════════════════════════════════════════════════");

        var builder = WebApplication.CreateBuilder(args);

        // Configuration
        _configuration = builder.Configuration;
        _pollingIntervalSeconds = int.Parse(_configuration["AppSettings:PollingIntervalSeconds"] ?? "60");
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        // Add services
        builder.Services.AddDbContext<DefaultdbContext>(options =>
            options.UseMySql(_connectionString, ServerVersion.AutoDetect(_connectionString)));

        // Add CORS for Admintool access
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll",
                builder => builder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader());
        });

        // Add background service for email polling
        builder.Services.AddSingleton<IConfiguration>(_configuration);
        builder.Services.AddHostedService<EmailPollingService>();

        // Register HttpClient for API calls
        builder.Services.AddHttpClient();

        // Add Swagger/OpenAPI support
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure middleware
        app.UseCors("AllowAll");

        // Enable Swagger middleware
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "UTXO E-Mail Agent API v1");
            options.RoutePrefix = "swagger"; // Swagger will be available at /swagger
        });

        // API endpoint for processing emails
        app.MapPost("/api/processtext", async (ProcessTextRequestClass request, DefaultdbContext db) =>
        {
            try
            {
                // Get agent (default to first active agent if not specified)
                Agent? agent;
                if (request.AgentId.HasValue)
                {
                    agent = await db.Agents
                        .Include(a => a.Mcpservers)
                        .Where(a => a.Id == request.AgentId.Value && a.State == "active")
                        .FirstOrDefaultAsync();
                }
                else
                {
                    agent = await db.Agents
                        .Include(a => a.Mcpservers)
                        .Where(a => a.State == "active")
                        .FirstOrDefaultAsync();
                }

                if (agent == null)
                {
                    return Results.BadRequest(new ProcessEmailResponse
                    {
                        Success = false,
                        Error = "No active agent found"
                    });
                }

                // Create mail object from request
                var mail = new MailClass
                {
                    Id = "API-" + Guid.NewGuid().ToString(),
                    Type = "email",
                    From = "api@example.com",
                    To = new[] { agent.Emailaddress },
                    Subject = "API Request",
                    Status = "unread",
                    CreatedAt = DateTime.Now.ToString("o"),
                    Text = request.TextContent,
                    Html = string.Empty,
                    Cc = Array.Empty<string>(),
                    Bcc = Array.Empty<string>(),
                    ReplyTo = Array.Empty<string>(),
                    Attachments = Array.Empty<string>()
                };

                // Process with AI
                var processor = new ProcessMailsClass(db, _configuration);
                var aiResponse = await processor.ProcessMailAsync(mail, agent);

                // Build response
                var response = new ProcessEmailResponse
                {
                    Success = true,
                    EmailResponseText = aiResponse.EmailResponseText,
                    EmailResponseSubject = aiResponse.EmailResponseSubject,
                    EmailResponseHtml = aiResponse.EmailResponseHtml,
                    AiExplanation = aiResponse.AiExplanation
                };

                // Add attachments to response
                if (aiResponse.Attachments != null)
                {
                    foreach (var att in aiResponse.Attachments)
                    {
                        response.Attachments.Add(new AttachmentResponse
                        {
                            Filename = att.Filename,
                            ContentType = att.ContentType,
                            Content = att.Content
                        });
                    }
                }

                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API] Error processing email: {ex.Message}");
                return Results.BadRequest(new ProcessEmailResponse
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        })
        .WithName("ProcessText")
        .WithSummary("Process text with AI")
        .Produces<ProcessEmailResponse>(StatusCodes.Status200OK)
        .Produces<ProcessEmailResponse>(StatusCodes.Status400BadRequest);

        // Health check endpoint
        app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", version = Version }))
            .WithName("HealthCheck")
            .WithSummary("Health check")
            .Produces(StatusCodes.Status200OK);

        Console.WriteLine($"Polling interval: {_pollingIntervalSeconds} seconds");
        Console.WriteLine("API running on: http://localhost:5051");
        Console.WriteLine("Endpoints:");
        Console.WriteLine("  POST /api/processtext - Process text with AI");
        Console.WriteLine("  GET /api/health - Health check");
        Console.WriteLine("");
        Console.WriteLine("Swagger UI available at: http://localhost:5051/swagger");

        // Run the web application
        await app.RunAsync("http://0.0.0.0:5051");
    }
    
}