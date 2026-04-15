using Microsoft.Extensions.FileProviders;
using PawMatch.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<MySqlConnectionFactory>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // Development: allow any origin (any Live Server port, Cursor preview, file:// Origin null, etc.)
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(_ => true)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(
                    "http://localhost:8765",
                    "http://127.0.0.1:8765",
                    "http://localhost:5500",
                    "http://127.0.0.1:5500")
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Serve ../frontend from the same origin as the API (e.g. open http://localhost:5102 for the SPA).
// Static files must run before UseRouting or GET / and /js/*.js may 404 (see ASP.NET Core static files docs).
var frontendPath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "frontend"));
PhysicalFileProvider? frontendFiles = null;
if (Directory.Exists(frontendPath))
{
    frontendFiles = new PhysicalFileProvider(frontendPath);
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = frontendFiles,
        RequestPath = ""
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = frontendFiles,
        RequestPath = ""
    });
}

// Routing + CORS order matters for browser preflight (OPTIONS) on POST /api/auth/*
app.UseRouting();
app.UseCors();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthorization();
app.MapControllers();

if (frontendFiles is not null)
{
    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        FileProvider = frontendFiles
    });
}

app.Run();
