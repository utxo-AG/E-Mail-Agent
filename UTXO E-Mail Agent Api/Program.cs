using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using UTXO_E_Mail_Agent_Api.Authentication;
using UTXO_E_Mail_Agent_Shared.Models;

var builder = WebApplication.CreateBuilder(args);

// Add Entity Framework
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<DefaultdbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
        mysqlOptions => mysqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

// Add controllers
builder.Services.AddControllers();

// Add HttpClient for webhook forwarding
builder.Services.AddHttpClient();

// Add API Key Authentication
builder.Services.AddAuthentication("ApiKey")
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("ApiKey", null);
builder.Services.AddAuthorization();

// Add Swagger with Bearer token support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "UTXO E-Mail Agent API",
        Version = "v1",
        Description = "API for managing email agents and conversations"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "API Key authentication using Bearer scheme. Enter your API key below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Exclude endpoints with ApiExplorerSettings(IgnoreApi = true)
    c.IgnoreObsoleteActions();
});

var app = builder.Build();

// Configure Swagger (available in all environments for API documentation)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "UTXO E-Mail Agent API v1");
    c.RoutePrefix = string.Empty; // Swagger at root
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
