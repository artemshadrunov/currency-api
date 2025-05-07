using ApiCurrency.Services;

namespace ApiCurrency;

public class Startup
{
    public IConfiguration Configuration { get; }

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        
        if (!IsProduction())
        {
            services.AddSwaggerGen();
        }

        services.AddScoped<ICurrencyConverterService, CurrencyConverterServiceMock>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (!IsProduction())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }

    private bool IsProduction()
    {
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";
    }
} 