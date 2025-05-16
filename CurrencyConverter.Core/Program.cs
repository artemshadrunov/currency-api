using CurrencyConverter.Core.Infrastructure;
using CurrencyConverter.Core.Settings;
using Serilog;
using CurrencyConverter.Core.Models;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure environment
    var environment = builder.Environment.EnvironmentName;
    builder.Configuration
        .SetBasePath(builder.Environment.ContentRootPath)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();

    // Configure settings
    builder.Services.Configure<CurrencyRulesOptions>(
        builder.Configuration.GetSection("CurrencyRules"));
    builder.Services.Configure<RedisSettings>(
        builder.Configuration.GetSection("Redis"));

    // Configure services
    ApplicationConfiguration.ConfigureServices(builder);

    var app = builder.Build();

    // Configure middleware
    ApplicationConfiguration.ConfigureMiddleware(app);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}