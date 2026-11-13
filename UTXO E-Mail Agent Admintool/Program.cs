using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using UTXO_E_Mail_Agent_Admintool.Components;
using UTXO_E_Mail_Agent_Shared.Models;
using UTXO_E_Mail_Agent_Admintool.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Increase SignalR message size limit for large system prompts (default: 32KB)
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 512 * 1024; // 512 KB
});

// Add MudBlazor services
builder.Services.AddMudServices();

// Add Data Protection
builder.Services.AddDataProtection();

// Add Session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add DbContext for MariaDB/MySQL database.
// Verbindung aus Umgebungsvariablen (DB_HOST/DB_USER/... oder UTXO_DB_CONNECTION)
// oder als Fallback aus appsettings.json.
var connectionString = DatabaseConfig.ResolveConnectionString(
    builder.Configuration.GetConnectionString("DefaultConnection"));

// Add DbContextFactory for Blazor components (replaces AddDbContext for better Blazor Server support)
builder.Services.AddDbContextFactory<DefaultdbContext>(options =>
    options.UseMySql(connectionString, DatabaseConfig.GetServerVersion(connectionString),
        mysqlOptions => mysqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

// Also register a scoped DbContext for services that need direct injection (like AuthService)
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<DefaultdbContext>>().CreateDbContext());

// Add custom authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.Cookie.Name = ".AspNetCore.Cookies"; // Standard ASP.NET Core cookie name
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient(); // For EmailService and other HTTP requests
builder.Services.AddScoped(sp =>
{
    var navigationManager = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(navigationManager.BaseUri) };
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider =>
    provider.GetRequiredService<CustomAuthenticationStateProvider>());
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<AgentApiService>();
builder.Services.AddScoped<Office365AuthService>();
builder.Services.AddScoped<SkillUploadService>();

builder.Services.AddAuthorization();

var app = builder.Build();

// Datenbank-Erstneuaufbau:
// Wenn die Umgebungsvariable DB_INIT=true gesetzt ist (oder das Argument --init-db
// übergeben wird), wird das komplette Schema aus dem Modell (DefaultdbContext) erzeugt.
// EnsureCreated() legt Datenbank + alle Tabellen an, falls sie noch nicht existieren,
// und lässt eine bereits vorhandene Datenbank unangetastet. Danach läuft das Tool normal weiter.
if (string.Equals(Environment.GetEnvironmentVariable("DB_INIT"), "true", StringComparison.OrdinalIgnoreCase)
    || args.Contains("--init-db"))
{
    using var initScope = app.Services.CreateScope();
    var contextFactory = initScope.ServiceProvider.GetRequiredService<IDbContextFactory<DefaultdbContext>>();
    await using var initDb = await contextFactory.CreateDbContextAsync();
    var created = await initDb.Database.EnsureCreatedAsync();
    Console.WriteLine(created
        ? "[DB-Init] Datenbank und Tabellen wurden neu erstellt."
        : "[DB-Init] Datenbank existiert bereits – kein Schema-Aufbau nötig.");

    // Standard-Administrator anlegen, falls noch keiner existiert.
    // EnsureCreated() legt nur das Schema an, keine Daten – ohne diesen Schritt
    // gäbe es keinen Login-Benutzer.
    if (!await initDb.Administrators.AnyAsync())
    {
        var passwordService = initScope.ServiceProvider.GetRequiredService<PasswordService>();

        var username = Environment.GetEnvironmentVariable("ADMIN_USERNAME");
        if (string.IsNullOrWhiteSpace(username))
        {
            username = "admin";
        }

        // Passwort aus ADMIN_PASSWORD übernehmen oder sicher generieren.
        var envPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
        var passwordWasGenerated = string.IsNullOrWhiteSpace(envPassword);
        var password = passwordWasGenerated ? passwordService.GeneratePassword(16) : envPassword!;

        initDb.Administrators.Add(new Administrator
        {
            Username = username,
            Passwordhash = passwordService.HashPassword(password),
            State = "active",
            Loginattempts = 0
        });
        await initDb.SaveChangesAsync();

        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine("[DB-Init] Standard-Administrator wurde angelegt:");
        Console.WriteLine($"[DB-Init]   Benutzername: {username}");
        if (passwordWasGenerated)
        {
            Console.WriteLine($"[DB-Init]   Passwort:     {password}");
            Console.WriteLine("[DB-Init]   >> Bitte sicher notieren und nach dem ersten Login ändern!");
        }
        else
        {
            Console.WriteLine("[DB-Init]   Passwort:     (aus ADMIN_PASSWORD übernommen)");
        }
        Console.WriteLine("═══════════════════════════════════════════════════");
    }
    else
    {
        Console.WriteLine("[DB-Init] Administrator(en) bereits vorhanden – kein neuer Benutzer angelegt.");
    }
}

// Automatische Schema-Migrationen: bei jedem Start werden ausstehende SQL-Schritte
// angewendet (getrackt in schema_migrations), sofern die Datenbank bereits existiert.
// So verteilen sich DB-Änderungen ohne direkten Datenbankzugriff von außen.
try
{
    using var migrateScope = app.Services.CreateScope();
    var migrateFactory = migrateScope.ServiceProvider.GetRequiredService<IDbContextFactory<DefaultdbContext>>();
    await using var migrateDb = await migrateFactory.CreateDbContextAsync();

    if (await migrateDb.Database.CanConnectAsync())
    {
        var applied = await DatabaseMigrator.ApplyAsync(migrateDb);
        Console.WriteLine(applied > 0
            ? $"[DB-Migrate] {applied} Migration(en) angewendet."
            : "[DB-Migrate] Schema ist aktuell – keine Migration nötig.");
    }
    else
    {
        Console.WriteLine("[DB-Migrate] Keine Datenbankverbindung – übersprungen.");
    }
}
catch (Exception ex)
{
    // Migrationen dürfen den Start nicht verhindern (z. B. wenn die DB noch nicht
    // initialisiert ist – dann zuerst mit DB_INIT=true starten).
    Console.WriteLine($"[DB-Migrate] Übersprungen (Fehler): {ex.Message}");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();