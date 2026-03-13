using OptiVis.Application;
using OptiVis.Infrastructure;
using OptiVis.API.Hubs;
using OptiVis.API.Workers;
using Serilog;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OptiVis API",
        Version = "v1",
        Description = "Call center analytics API"
    });
});

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddHostedService<CdrPollingWorker>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });

    options.AddPolicy("SignalR", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseSerilogRequestLogging();

app.UseSwagger(options =>
{
    options.RouteTemplate = "openapi/{documentName}.json";
});

if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "OptiVis API v1");
    });
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("OptiVis API");
        options.WithTheme(ScalarTheme.BluePlanet);
        options.WithOpenApiRoutePattern("/openapi/{documentName}.json");
    });
}

app.UseCors();

app.Map("/", () => Results.Ok("OptiVis API is running"));

app.MapControllers();
app.MapHub<DashboardHub>("/hubs/dashboard").RequireCors("SignalR");

app.Run();
