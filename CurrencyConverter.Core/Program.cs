using ApiCurrency.Models;
using ApiCurrency.Services;
using ApiCurrency.Settings;
using ApiCurrency.ExchangeRateProviders;
using CurrencyConverter.Core.Infrastructure.Cache;
using CurrencyConverter.Core.Settings;
using CurrencyConverter.Core.ExchangeRateProviders;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using Microsoft.Extensions.Caching.StackExchangeRedis;

var builder = WebApplication.CreateBuilder(args);

var environment = builder.Configuration["ASPNETCORE_ENVIRONMENT"];
if (!string.IsNullOrEmpty(environment))
{
    builder.Environment.EnvironmentName = environment;
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Always enable Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Currency Converter API",
        Version = "v1",
        Description = "API for currency conversion"
    });
});

// Configure settings
builder.Services.Configure<CurrencyRulesOptions>(
    builder.Configuration.GetSection("CurrencyRules"));
builder.Services.Configure<RedisSettings>(
    builder.Configuration.GetSection("Redis"));

// Configure Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<RedisSettings>>().Value;
    return ConnectionMultiplexer.Connect(settings.ConnectionString);
});

// Add Redis distributed cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    var settings = builder.Configuration.GetSection("Redis").Get<RedisSettings>();
    options.Configuration = settings.ConnectionString;
    options.InstanceName = settings.InstanceName;
});

// Register cache services
builder.Services.AddSingleton<ICacheProvider, RedisCacheProvider>();
builder.Services.AddSingleton<CachedExchangeRateProviderFactory>();

// Register HTTP client
builder.Services.AddHttpClient();

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
builder.Services.AddSingleton<IExchangeRateProviderFactory>(sp =>
{
    var cache = sp.GetRequiredService<ICacheProvider>();
    var settings = sp.GetRequiredService<IOptions<RedisSettings>>();
    var providers = sp.GetServices<IExchangeRateProvider>();
    return new CachedExchangeRateProviderFactory(cache, settings, providers);
});

// Register main service
builder.Services.AddScoped<ICurrencyConverterService, CurrencyConverterService>();

var app = builder.Build();

// Always enable Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Currency Converter API V1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();