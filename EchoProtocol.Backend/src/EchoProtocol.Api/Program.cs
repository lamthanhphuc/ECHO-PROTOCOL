using System.Text;
using System.Text.Json.Serialization;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Health;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var response = ApiResponse<object>.Fail("Validation failed", ErrorCodes.ValidationError);
        return new BadRequestObjectResult(response);
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ECHO PROTOCOL API",
        Version = "v1",
        Description = "Backend API for ECHO PROTOCOL"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT token (raw token only; Swagger adds 'Bearer' prefix)"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            Array.Empty<string>()
        }
    });
});

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.Configure<AdminSeedSettings>(
    builder.Configuration.GetSection(AdminSeedSettings.SectionName));
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection(MongoDbSettings.SectionName));
builder.Services.Configure<MatchAuthoritySettings>(
    builder.Configuration.GetSection(MatchAuthoritySettings.SectionName));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDb")
    ?? throw new InvalidOperationException("Connection string 'MongoDb' not found.");
var mongoDbSettings = builder.Configuration
    .GetSection(MongoDbSettings.SectionName)
    .Get<MongoDbSettings>()
    ?? throw new InvalidOperationException("MongoDB settings not configured.");

ValidateMongoDbSettings(mongoDbSettings);

builder.Services.AddSingleton<IMongoClient>(_ =>
{
    var settings = MongoClientSettings.FromConnectionString(mongoConnectionString);
    settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
    return new MongoClient(settings);
});
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase(mongoDbSettings.DatabaseName));
builder.Services.AddSingleton<ITelemetryEventRepository, MongoTelemetryEventRepository>();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("postgresql")
    .AddCheck<MongoDatabaseHealthCheck>("mongodb");

var jwtSettings = builder.Configuration
    .GetSection(JwtSettings.SectionName)
    .Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings not configured.");

ValidateJwtSettings(jwtSettings);

var matchAuthoritySettings = builder.Configuration
    .GetSection(MatchAuthoritySettings.SectionName)
    .Get<MatchAuthoritySettings>()
    ?? throw new InvalidOperationException("Match authority settings not configured.");
ValidateMatchAuthoritySettings(matchAuthoritySettings);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                if (context.Response.HasStarted)
                {
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                string errorCode;
                string message;

                if (context.AuthenticateFailure is not null)
                {
                    errorCode = ErrorCodes.TokenInvalid;
                    message = "Invalid or expired token";
                }
                else
                {
                    errorCode = ErrorCodes.Unauthorized;
                    message = "Unauthorized";
                }

                await context.Response.WriteAsJsonAsync(
                    ApiResponse<object>.Fail(message, errorCode));
            },
            OnForbidden = async context =>
            {
                if (context.Response.HasStarted)
                {
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(
                    ApiResponse<object>.Fail("Forbidden", ErrorCodes.Forbidden));
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<ITelemetryService, TelemetryService>();
builder.Services.AddScoped<IMatchAuthorityService, MatchAuthorityService>();
builder.Services.AddSingleton<IMatchJoinProofService, MatchJoinProofService>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddCors(options =>
{
    options.AddPolicy("EchoProtocolDev", policy =>
    {
        policy.WithOrigins(
                "http://localhost",
                "http://127.0.0.1")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

try
{
    await MongoTelemetryInitializer.InitializeAsync(
        app.Services.GetRequiredService<ITelemetryEventRepository>());
}
catch (MongoException ex)
{
    app.Logger.LogWarning(
        ex,
        "MongoDB telemetry initialization failed; Auth and PostgreSQL endpoints remain available.");
}
catch (TimeoutException ex)
{
    app.Logger.LogWarning(
        ex,
        "MongoDB telemetry initialization timed out; Auth and PostgreSQL endpoints remain available.");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var startupLogger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");

    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        await DbInitializer.InitializeAsync(
            db,
            scope.ServiceProvider.GetRequiredService<IOptions<AdminSeedSettings>>(),
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer"));
    }
    catch (Exception ex)
    {
        startupLogger.LogError(ex, "Development database migration or seed failed.");
        throw;
    }
}

app.UseHttpsRedirection();
app.UseCors("EchoProtocolDev");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static void ValidateJwtSettings(JwtSettings settings)
{
    if (string.IsNullOrWhiteSpace(settings.SecretKey))
    {
        throw new InvalidOperationException("JWT SecretKey is not configured.");
    }

    if (Encoding.UTF8.GetBytes(settings.SecretKey).Length < 32)
    {
        throw new InvalidOperationException(
            "JWT SecretKey must be at least 32 bytes when UTF-8 encoded.");
    }

    if (string.IsNullOrWhiteSpace(settings.Issuer))
    {
        throw new InvalidOperationException("JWT Issuer is not configured.");
    }

    if (string.IsNullOrWhiteSpace(settings.Audience))
    {
        throw new InvalidOperationException("JWT Audience is not configured.");
    }

    if (settings.ExpiryMinutes <= 0)
    {
        throw new InvalidOperationException("JWT ExpiryMinutes must be greater than zero.");
    }
}

static void ValidateMongoDbSettings(MongoDbSettings settings)
{
    if (string.IsNullOrWhiteSpace(settings.DatabaseName))
    {
        throw new InvalidOperationException("MongoDb:DatabaseName is not configured.");
    }

    if (string.IsNullOrWhiteSpace(settings.TelemetryCollectionName))
    {
        throw new InvalidOperationException("MongoDb:TelemetryCollectionName is not configured.");
    }

    if (settings.MaxBatchSize is < 1 or > 5000)
    {
        throw new InvalidOperationException("MongoDb:MaxBatchSize must be between 1 and 5000.");
    }

    if (string.IsNullOrWhiteSpace(settings.SupportedSchemaVersion))
    {
        throw new InvalidOperationException("MongoDb:SupportedSchemaVersion is not configured.");
    }

    if (settings.MaxValueJsonBytes is < 256 or > 1_048_576)
    {
        throw new InvalidOperationException("MongoDb:MaxValueJsonBytes must be between 256 and 1048576.");
    }

    if (settings.MaxFutureSkewMinutes is < 0 or > 60)
    {
        throw new InvalidOperationException("MongoDb:MaxFutureSkewMinutes must be between 0 and 60.");
    }

    if (settings.MaxEventAgeDays is < 1 or > 365)
    {
        throw new InvalidOperationException("MongoDb:MaxEventAgeDays must be between 1 and 365.");
    }
}

static void ValidateMatchAuthoritySettings(MatchAuthoritySettings settings)
{
    if (Encoding.UTF8.GetByteCount(settings.ProofSigningKey) < 32)
    {
        throw new InvalidOperationException(
            "MatchAuthority ProofSigningKey must be at least 32 bytes when UTF-8 encoded.");
    }

    if (settings.JoinProofLifetimeSeconds is < 30 or > 600)
    {
        throw new InvalidOperationException(
            "MatchAuthority JoinProofLifetimeSeconds must be between 30 and 600.");
    }

    if (settings.LeaseLifetimeSeconds is < 15 or > 300)
    {
        throw new InvalidOperationException(
            "MatchAuthority LeaseLifetimeSeconds must be between 15 and 300.");
    }

    if (settings.TelemetryDelegationRetentionHours is < 1 or > 168)
    {
        throw new InvalidOperationException(
            "MatchAuthority TelemetryDelegationRetentionHours must be between 1 and 168.");
    }
}
