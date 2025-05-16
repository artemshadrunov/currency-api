using CurrencyConverter.Core.Infrastructure.Cache;
using CurrencyConverter.Core.Models;
using CurrencyConverter.Core.Services;
using CurrencyConverter.Core.Settings;
using CurrencyConverter.Core.ExchangeRateProviders;
using CurrencyConverter.Core.Infrastructure;
using CurrencyConverter.Core.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;
using System.Text;
using Serilog;

try
{
    var builder = WebApplication.CreateBuilder(args);

    var environment = builder.Configuration["ASPNETCORE_ENVIRONMENT"];
    if (!string.IsNullOrEmpty(environment))
    {
        builder.Environment.EnvironmentName = environment;
    }

    // Configure settings
    builder.Services.Configure<CurrencyRulesOptions>(
        builder.Configuration.GetSection("CurrencyRules"));
    builder.Services.Configure<RedisSettings>(
        builder.Configuration.GetSection("Redis"));

    // Configure services
    ApplicationConfiguration.ConfigureServices(builder);

    // Register cache services
    builder.Services.AddSingleton<ICacheProvider, RedisCacheProvider>();
    builder.Services.AddSingleton<CachedExchangeRateProviderFactory>();

    // Register providers
    builder.Services.AddSingleton<ICurrencyRulesProvider, CurrencyRulesSettingsProvider>();

    // Register cached providers
    builder.Services.AddSingleton<IExchangeRateProvider>(sp =>
    {
        var cache = sp.GetRequiredService<ICacheProvider>();
        var settings = sp.GetRequiredService<IOptions<RedisSettings>>();
        var logger = sp.GetRequiredService<ILogger<CachedExchangeRateProvider>>();
        var stubProvider = new StubExchangeRateProvider();
        return new CachedExchangeRateProvider(stubProvider, cache, settings, logger);
    });

    builder.Services.AddSingleton<IExchangeRateProvider>(sp =>
    {
        var cache = sp.GetRequiredService<ICacheProvider>();
        var settings = sp.GetRequiredService<IOptions<RedisSettings>>();
        var logger = sp.GetRequiredService<ILogger<CachedExchangeRateProvider>>();
        var httpClient = sp.GetRequiredService<HttpClient>();
        var frankfurterLogger = sp.GetRequiredService<ILogger<FrankfurterExchangeRateProvider>>();
        var frankfurterProvider = new FrankfurterExchangeRateProvider(httpClient, frankfurterLogger);
        return new CachedExchangeRateProvider(frankfurterProvider, cache, settings, logger);
    });

    // Register provider factory
    builder.Services.AddSingleton<IExchangeRateProviderFactory, CachedExchangeRateProviderFactory>();

    // Register main service
    builder.Services.AddScoped<ICurrencyConverterService, CurrencyConverterService>();

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