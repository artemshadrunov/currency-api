using ApiCurrency.Models;
using ApiCurrency.Services;
using ApiCurrency.Settings;
using ApiCurrency.ExchangeRateProviders;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var environment = builder.Configuration["ASPNETCORE_ENVIRONMENT"];
if (!string.IsNullOrEmpty(environment))
{
    builder.Environment.EnvironmentName = environment;
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Currency Converter API",
            Version = "v1",
            Description = "API for currency conversion"
        });
    });
}

builder.Services.Configure<ExchangeRateSettings>(
    builder.Configuration.GetSection("ExchangeRateSettings"));
builder.Services.Configure<CurrencyRulesOptions>(
    builder.Configuration.GetSection("CurrencyRules"));

builder.Services.AddHttpClient();
builder.Services.AddSingleton<ICurrencyRulesProvider, CurrencyRulesSettingsProvider>();
builder.Services.AddSingleton<IExchangeRateProvider, StubExchangeRateProvider>();
builder.Services.AddSingleton<IExchangeRateProvider>(sp =>
    new FrankfurterExchangeRateProvider(
        sp.GetRequiredService<HttpClient>(),
        sp.GetRequiredService<IOptions<ExchangeRateSettings>>()));
builder.Services.AddSingleton<IExchangeRateProviderFactory>(sp =>
{
    var providers = sp.GetServices<IExchangeRateProvider>();
    return new ExchangeRateProviderFactory(providers);
});
builder.Services.AddScoped<ICurrencyConverterService, CurrencyConverterService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Currency Converter API V1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();