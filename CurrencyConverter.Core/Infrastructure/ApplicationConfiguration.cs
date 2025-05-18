using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using CurrencyConverter.Core.Settings;
using System.Threading.RateLimiting;
using Serilog;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using CurrencyConverter.Core.Infrastructure.Cache;
using CurrencyConverter.Core.ExchangeRateProviders;
using CurrencyConverter.Core.Services;
using Polly;
using Polly.Extensions.Http;
using Polly.CircuitBreaker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Polly.Registry;
using CurrencyConverter.Core.Infrastructure.Http;
using Microsoft.Extensions.Configuration;

namespace CurrencyConverter.Core.Infrastructure;

public static class ApplicationConfiguration
{
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // 1. Infrastructure Services
        ConfigureLogging(services, configuration);
        ConfigureOpenTelemetry(services, configuration);
        ConfigureRedis(services, configuration);
        ConfigureResiliencePolicies(services);

        // 2. Security Services
        ConfigureAuthentication(services, configuration);
        ConfigureAuthorization(services);
        ConfigureRateLimiting(services);

        // 3. API Services
        ConfigureApiVersioning(services);
        ConfigureSwagger(services, configuration);

        // 4. Application Services
        ConfigureCoreServices(services);
        ConfigureExchangeRateProviders(services);
    }

    private static void ConfigureLogging(IServiceCollection services, IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        services.AddLogging(loggingBuilder =>
            loggingBuilder.AddSerilog(dispose: true));
    }

    private static void ConfigureOpenTelemetry(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenTelemetry()
            .WithTracing(tracerProviderBuilder =>
                tracerProviderBuilder
                    .AddSource("CurrencyConverter")
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService("CurrencyConverter", serviceVersion: "1.0.0"))
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddRedisInstrumentation()
                    .AddConsoleExporter()
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317");
                    }));
    }

    private static void ConfigureRedis(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<RedisSettings>>().Value;
            var options = ConfigurationOptions.Parse(settings.ConnectionString);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });

        services.AddStackExchangeRedisCache(options =>
        {
            var redisSettings = configuration.GetSection("Redis").Get<RedisSettings>() ?? new RedisSettings();
            options.Configuration = redisSettings.ConnectionString;
            options.InstanceName = redisSettings.InstanceName;
        });
    }

    private static void ConfigureResiliencePolicies(IServiceCollection services)
    {
        // Configure retry policy with exponential backoff
        var retryPolicy = Polly.Extensions.Http.HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        // Configure circuit breaker policy
        var circuitBreakerPolicy = Polly.Extensions.Http.HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30)
            );

        // Combine policies
        var combinedPolicy = Polly.Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);

        // Register policies
        services.AddHttpClient("ResilientHttpClient")
            .AddPolicyHandler(combinedPolicy);

        // Register policy registry for use in other services
        services.AddPolicyRegistry(new Polly.Registry.PolicyRegistry
        {
            { "RetryPolicy", retryPolicy },
            { "CircuitBreakerPolicy", circuitBreakerPolicy },
            { "CombinedPolicy", combinedPolicy }
        });
    }

    private static void ConfigureAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"],
                ValidAudience = configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"] ?? string.Empty))
            };
        });
    }

    private static void ConfigureAuthorization(IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("User", policy => policy.RequireRole("User", "Admin"));
            options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
        });
    }

    private static void ConfigureRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", token);
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1)
                    }));
        });
    }

    private static void ConfigureApiVersioning(IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });

        services.AddVersionedApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });
    }

    private static void ConfigureSwagger(IServiceCollection services, IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer();

        if (!IsProduction())
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Currency Converter API",
                    Version = "v1",
                    Description = "API for currency conversion"
                });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    In = ParameterLocation.Header,
                    Description = "Please enter a valid token",
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    BearerFormat = "JWT",
                    Scheme = "bearer"
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        new string[] {}
                    }
                });
            });
        }
    }

    private static void ConfigureCoreServices(IServiceCollection services)
    {
        services.AddControllers();
        services.AddHttpClient();
        services.AddScoped<ICurrencyConverterService, CurrencyConverterService>();

        // Register ResilientHttpClient
        services.AddSingleton<IHttpClient>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("ResilientHttpClient");
            var policyRegistry = sp.GetRequiredService<IReadOnlyPolicyRegistry<string>>();
            return new ResilientHttpClient(httpClient, policyRegistry);
        });
    }

    private static void ConfigureExchangeRateProviders(IServiceCollection services)
    {
        // Register cache services
        services.AddSingleton<ICacheProvider, RedisCacheProvider>();
        services.AddSingleton<CachedExchangeRateProviderFactory>();

        // Register providers
        services.AddSingleton<ICurrencyRulesProvider, CurrencyRulesSettingsProvider>();

        // Register cached providers
        services.AddSingleton<IExchangeRateProvider>(sp =>
        {
            var cache = sp.GetRequiredService<ICacheProvider>();
            var settings = sp.GetRequiredService<IOptions<RedisSettings>>();
            var logger = sp.GetRequiredService<ILogger<CachedExchangeRateProvider>>();
            var stubProvider = new StubExchangeRateProvider();
            return new CachedExchangeRateProvider(stubProvider, cache, settings, logger);
        });

        services.AddSingleton<IExchangeRateProvider>(sp =>
        {
            var cache = sp.GetRequiredService<ICacheProvider>();
            var settings = sp.GetRequiredService<IOptions<RedisSettings>>();
            var logger = sp.GetRequiredService<ILogger<CachedExchangeRateProvider>>();
            var httpClient = sp.GetRequiredService<IHttpClient>();
            var frankfurterLogger = sp.GetRequiredService<ILogger<FrankfurterExchangeRateProvider>>();
            var frankfurterProvider = new FrankfurterExchangeRateProvider(httpClient, frankfurterLogger);
            return new CachedExchangeRateProvider(frankfurterProvider, cache, settings, logger);
        });

        // Register provider factory
        services.AddSingleton<IExchangeRateProviderFactory, CachedExchangeRateProviderFactory>();
    }

    public static void ConfigureMiddleware(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (!IsProduction())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Currency Converter API V1");
                c.RoutePrefix = "swagger";
            });
        }

        app.UseHttpsRedirection();
        app.UseRateLimiter();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseAuthentication();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseRouting();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }

    private static bool IsProduction()
    {
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";
    }
}