using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CurrencyConverter.Core.Infrastructure;
using CurrencyConverter.Core.Settings;
using CurrencyConverter.Core.Models;

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

            // Base services
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            services.AddHttpClient();
            services.AddMemoryCache();
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = Configuration.GetConnectionString("Redis");
                options.InstanceName = Configuration["Redis:InstanceName"];
            });

            // Business logic
            ApplicationConfiguration.ConfigureServices(services, Configuration);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            // Business logic middleware
            ApplicationConfiguration.ConfigureMiddleware(app, env);
        }
    }
}