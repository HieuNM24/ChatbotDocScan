using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using backend.Data;
using backend.Services;

// ──────────────────────────────────────────
// 0. Load .env file (if exists) for local development
// ──────────────────────────────────────────
var envPaths = new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
    Path.Combine(Directory.GetCurrentDirectory(), "..", ".env")
};

foreach (var envPath in envPaths)
{
    if (File.Exists(envPath))
    {
        foreach (var line in File.ReadAllLines(envPath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#')) continue;
            var parts = trimmed.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var val = parts[1].Trim();
                Environment.SetEnvironmentVariable(key, val);
            }
        }
        break;
    }
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// ──────────────────────────────────────────
// 1. Configuration
// ──────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// ──────────────────────────────────────────
// 2. Database (PostgreSQL + pgvector)
// ──────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions.UseVector()));

// ──────────────────────────────────────────
// 3. CORS – configured origins
// ──────────────────────────────────────────
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ──────────────────────────────────────────
// 4. Application Services
// ──────────────────────────────────────────
builder.Services.AddScoped<PdfService>();
builder.Services.AddHttpClient<GeminiService>();

// ──────────────────────────────────────────
// 5. Controllers & OpenAPI
// ──────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// ──────────────────────────────────────────
// 6. Global Exception Handling Middleware
// ──────────────────────────────────────────
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();

        if (exceptionFeature?.Error != null)
        {
            logger.LogError(exceptionFeature.Error, "Unhandled exception on {Path}", exceptionFeature.Path);
        }

        await context.Response.WriteAsJsonAsync(new
        {
            status = 500,
            message = "Đã xảy ra lỗi máy chủ nội bộ. Vui lòng thử lại sau."
        });
    });
});

// ──────────────────────────────────────────
// 7. Auto-migrate database on startup
// ──────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// ──────────────────────────────────────────
// 8. Middleware pipeline
// ──────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("FrontendPolicy");
app.UseAuthorization();
app.MapControllers();

app.Run();
