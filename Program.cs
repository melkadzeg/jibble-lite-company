using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Prefer env var DB_CONN; fallback to appsettings.json
var conn = Environment.GetEnvironmentVariable("DB_CONN")
           ?? builder.Configuration.GetConnectionString("Default")
           ?? throw new InvalidOperationException("DB connection string missing.");

builder.Services.AddDbContext<CompanyDb>(opt =>
    opt.UseNpgsql(conn, npg => npg.EnableRetryOnFailure()));

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// Auto-migrate on boot (dev only)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CompanyDb>();
    db.Database.Migrate();
}

app.MapControllers();
app.MapGet("/health", () => "ok");

app.Run();