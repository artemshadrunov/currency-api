using ApiCurrency.Models;
using ApiCurrency.Services;
using ApiCurrency.Settings;
using ApiCurrency.ExchangeRateProviders;
using CurrencyConverter.Core.Infrastructure.Cache;
using CurrencyConverter.Core.Settings;
using CurrencyConverter.Core.ExchangeRateProviders;
using CurrencyConverter.Core.Infrastructure;
using Microsoft.Extensions.Options;

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
    var stubProvider = new StubExchangeRateProvider();
    return new CachedExchangeRateProvider(stubProvider, cache, settings);
});

builder.Services.AddSingleton<IExchangeRateProvider>(sp =>
{
    var cache = sp.GetRequiredService<ICacheProvider>();
    var settings = sp.GetRequiredService<IOptions<RedisSettings>>();
    var httpClient = sp.GetRequiredService<HttpClient>();
    var frankfurterProvider = new FrankfurterExchangeRateProvider(httpClient);
    return new CachedExchangeRateProvider(frankfurterProvider, cache, settings);
});

// Register provider factory
builder.Services.AddSingleton<IExchangeRateProviderFactory, CachedExchangeRateProviderFactory>();

// Register main service
builder.Services.AddScoped<ICurrencyConverterService, CurrencyConverterService>();

var app = builder.Build();

// Configure middleware
ApplicationConfiguration.ConfigureMiddleware(app);

app.Run();