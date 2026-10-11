using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using RideMatching.Api.Middleware;
using RideMatching.Application.Interfaces;
using RideMatching.Infrastructure;
using RideMatching.Infrastructure.Data;
using RideMatching.Infrastructure.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers with enum string serialization
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Add SignalR for real-time duplex communication
builder.Services.AddSignalR();

// Add Infrastructure & Domain services
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add CORS for Angular frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Real-Time Ride Matching API",
        Version = "v1",
        Description = "Production-grade backend system for real-time ride matching with geospatial proximity indexing, optimistic concurrency, and SignalR duplex communication.",
        Contact = new OpenApiContact
        {
            Name = "RideShare Engineering"
        }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Global Exception Handler
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Enable Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ride Matching API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("FrontendPolicy");

app.MapControllers();
app.MapHub<RideHub>("/hubs/rides");

// Seed initial database on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var geoCache = scope.ServiceProvider.GetRequiredService<IDriverGeoCache>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await DbSeeder.SeedAsync(context, geoCache, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding initial data.");
    }
}

app.Run();
