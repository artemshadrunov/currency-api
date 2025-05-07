using ApiCurrency.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Set environment from configuration
var environment = builder.Configuration["ASPNETCORE_ENVIRONMENT"];
if (!string.IsNullOrEmpty(environment))
{
    builder.Environment.EnvironmentName = environment;
}

// Add services to the container.
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

builder.Services.AddScoped<ICurrencyConverterService, CurrencyConverterServiceMock>();

var app = builder.Build();

// Configure the HTTP request pipeline.
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