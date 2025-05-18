using CurrencyConverter.Core.Infrastructure;
using CurrencyConverter.Core.Settings;
using CurrencyConverter.Core.Models;
using System.Text.Json.Serialization;

namespace CurrencyConverter.Core
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public IWebHostEnvironment Environment { get; }

        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Configure settings
            services.Configure<CurrencyRulesOptions>(
                Configuration.GetSection("CurrencyRules"));
            services.Configure<RedisSettings>(
                Configuration.GetSection("Redis"));

            // Configure all services through ApplicationConfiguration
            ApplicationConfiguration.ConfigureServices(services, Configuration);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseMiddleware<GlobalExceptionHandler>();
            ApplicationConfiguration.ConfigureMiddleware(app, env);
        }
    }
}