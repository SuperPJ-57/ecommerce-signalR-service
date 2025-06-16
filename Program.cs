using EcommerceSignalrService;
using EcommerceSignalrService.Hubs;
using EcommerceSignalrService.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Polly;
using System.Security.Claims;
using System.Text;

namespace RealTimeLocationAPI;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();

        // Configure Swagger
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "v1",
                Title = "GetInstantMart SignalR Api",
                Description = "This API is responsible for notification service"
            });

            // Add Bearer Auth for Swagger
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Input your Bearer token like: Bearer {your token}"
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
                    new List<string>()
                }
            });
        });

        var jwtUserSettings = builder.Configuration.GetSection("JwtSettings:User").Get<JwtSettings>();
        var jwtServiceSettings = builder.Configuration.GetSection("JwtSettings:Service").Get<JwtSettings>();
        builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));



        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = "UserScheme";
            options.DefaultChallengeScheme = "UserScheme";
        })
            .AddJwtBearer("UserScheme", options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtUserSettings.Issuer,
                    ValidAudience = jwtUserSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtUserSettings.Key)),
                    RoleClaimType = ClaimTypes.Role
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/notificationhub"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            })
            .AddJwtBearer("ServiceScheme", options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtServiceSettings.Issuer,
                    ValidAudience = jwtServiceSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtServiceSettings.Key))
                };
            });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("UserPolicy", policy =>
            {
                policy.AuthenticationSchemes.Add("UserScheme");
                policy.RequireAuthenticatedUser();

            });

            options.AddPolicy("ServicePolicy", policy =>
            {
                policy.AuthenticationSchemes.Add("ServiceScheme");
                policy.RequireAuthenticatedUser();
            });
        }); 

        builder.Services.AddSignalR();
        builder.Services.AddSingleton<ConnectedUserManager>();
        builder.Services.AddHttpClient();
        builder.Services.AddHttpClient("BackendClient")
        .AddTransientHttpErrorPolicy(policy =>
            policy.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
        );
        var app = builder.Build();

        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseCors(x => x.AllowAnyMethod()
            .AllowAnyHeader()
            .SetIsOriginAllowed(o => true)
            .AllowCredentials());

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<NotificationHub>("/hubs/notificationhub")
            .RequireAuthorization("UserPolicy");  // <- Enforce only UserScheme clients
       app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));;
        app.Run();
    }
}
