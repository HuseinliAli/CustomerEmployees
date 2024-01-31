﻿using AspNetCoreRateLimit;
using CompanyEmloyees.Presentation.Controllers;
using Contracts.Logging;
using Contracts.Managers;
using Entities.Models;
using LoggerService.NLog;
using Marvin.Cache.Headers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Microsoft.IdentityModel.Tokens;
using Repositories.Contexts;
using Repositories.Managers;
using Services;
using Services.Contracts;
using System.Text;

namespace API.Extensions;
public static class ServiceExtensions
{
    public static void ConfigureCors(this IServiceCollection services)
        => services.AddCors(opt =>
           {
               opt.AddPolicy("CorsPolicy",
                   builder => builder.AllowAnyOrigin()
                                     .AllowAnyMethod()
                                     .AllowAnyHeader()
                                     .WithExposedHeaders("X-Pagination"));

           });

    public static void ConfigureISSIntegration(this IServiceCollection services)
        => services.Configure<IISOptions>(opt => { });

    public static void ConfigureLoggerService(this IServiceCollection services)
        => services.AddSingleton<ILoggerManager, NLogLoggerManager>();

    public static void ConfigureRepositoryManager(this IServiceCollection servies)
        => servies.AddScoped<IRepositoryManager, RepositoryManager>();

    public static void ConfigureServiceManager(this IServiceCollection servies)
      => servies.AddScoped<IServiceManager, ServiceManager>();

    public static void ConfigureSqlContext(this IServiceCollection services, IConfiguration configuration)
    => services.AddSqlServer<RepositoryContext>((configuration
        .GetConnectionString("sqlConnection")));

    public static void AddCustomMediaTypes(this IServiceCollection services)
    {
        services.Configure<MvcOptions>(config =>
        {
            var systemTextJsonOutputFormatter = config.OutputFormatters
                .OfType<SystemTextJsonOutputFormatter>()?.FirstOrDefault();

            if (systemTextJsonOutputFormatter !=null)
            {
                systemTextJsonOutputFormatter.SupportedMediaTypes.Add("application/vnd.codemaze.hateoas+json");
                systemTextJsonOutputFormatter.SupportedMediaTypes.Add("application/vnd.codemaze.apiroot+json");
            }


            var xmlOutputFormatter = config.OutputFormatters
                .OfType<XmlDataContractSerializerOutputFormatter>()?.FirstOrDefault();

            if (xmlOutputFormatter != null)
            {
                xmlOutputFormatter.SupportedMediaTypes.Add("application/vnd.codemaze.hateoas+json");
                xmlOutputFormatter.SupportedMediaTypes.Add("application/vnd.codemaze.apiroot+json");
            }
        });
    }

    public static void ConfigureVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(opt =>
        {
            opt.ReportApiVersions=true;
            opt.AssumeDefaultVersionWhenUnspecified=true;
            opt.DefaultApiVersion=new ApiVersion(1, 0);
            opt.ApiVersionReader = new HeaderApiVersionReader("api-version");

            opt.Conventions.Controller<CompaniesController>()
                .HasApiVersion(new ApiVersion(1, 0));
            opt.Conventions.Controller<EmployeesController>()
                .HasApiVersion(new ApiVersion(2, 0));
        });
    }

    public static void ConfigureResponseCaching(this IServiceCollection services)
        => services.AddResponseCaching();

    public static void ConfigureHttpCacheHeaders(this IServiceCollection services)
        => services.AddHttpCacheHeaders(expriationOpt =>
        {
            expriationOpt.MaxAge=65;
            expriationOpt.CacheLocation=CacheLocation.Private;
        },
        validationOpt =>
        {
            validationOpt.MustRevalidate=true;
        });

    public static void ConfigureRateLimitingOptions(this IServiceCollection services)
    {
        var rateLimitRules = new List<RateLimitRule> { new RateLimitRule { Endpoint="*", Limit=100, Period="5m" } };
        services.Configure<IpRateLimitOptions>(opt => { opt.GeneralRules=rateLimitRules; });
        services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
        services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
        services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
    }

    public static void ConfigureIdentiy(this IServiceCollection services)
    {
        var builder = services.AddIdentity<User, IdentityRole>(opt =>
        {
            opt.Password.RequireDigit=false;
            opt.Password.RequireLowercase=false;
            opt.Password.RequireUppercase=false;
            opt.Password.RequireNonAlphanumeric=false;
            opt.Password.RequiredLength = 3;
            opt.User.RequireUniqueEmail=true;

        })
            .AddEntityFrameworkStores<RepositoryContext>()
            .AddDefaultTokenProviders();
    }

    public static void ConfigureJWT(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");

        services.AddAuthentication(opt =>
        {
            opt.DefaultAuthenticateScheme=JwtBearerDefaults.AuthenticationScheme;
            opt.DefaultChallengeScheme=JwtBearerDefaults.AuthenticationScheme;
        })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters=new TokenValidationParameters
                {
                    ValidateIssuer=true,
                    ValidateAudience=true,
                    ValidateLifetime=true,
                    ValidateIssuerSigningKey=true,

                    ValidIssuer=jwtSettings["validIssuer"],
                    ValidAudience=jwtSettings["validAudience"],
                    IssuerSigningKey=new SymmetricSecurityKey
                    (Encoding.UTF8.GetBytes(jwtSettings["secret"]))
                };
            });
    }
}

