using System.Text;
using CongNoGolden.Api.Endpoints;
using CongNoGolden.Api.Middleware;
using CongNoGolden.Api.Security;
using CongNoGolden.Api.Services;
using CongNoGolden.Application.Auth;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Services;
using CongNoGolden.Infrastructure.Services.Common;
using CongNoGolden.Infrastructure.Migrations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration));

var observabilityServiceName = builder.Configuration["Observability:ServiceName"];
if (string.IsNullOrWhiteSpace(observabilityServiceName))
{
    observabilityServiceName = "congno-api";
}

var observabilityConsoleExporter = builder.Configuration.GetValue<bool>("Observability:EnableConsoleExporter");
var otlpEndpointRaw = builder.Configuration["Observability:OtlpEndpoint"];
var observabilityPrometheusExporter = builder.Configuration.GetValue<bool>("Observability:EnablePrometheusExporter");
var observabilityPrometheusPathRaw = builder.Configuration["Observability:PrometheusScrapeEndpointPath"];
var observabilityPrometheusPath = string.IsNullOrWhiteSpace(observabilityPrometheusPathRaw)
    ? "/metrics"
    : observabilityPrometheusPathRaw.Trim();
if (!observabilityPrometheusPath.StartsWith('/'))
{
    observabilityPrometheusPath = $"/{observabilityPrometheusPath}";
}
Uri? observabilityOtlpEndpoint = null;
if (!string.IsNullOrWhiteSpace(otlpEndpointRaw) &&
    Uri.TryCreate(otlpEndpointRaw, UriKind.Absolute, out var parsedOtlpEndpoint))
{
    observabilityOtlpEndpoint = parsedOtlpEndpoint;
}

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<ReadModelCacheOptions>(builder.Configuration.GetSection("ReadModelCache"));
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "congno:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(observabilityServiceName))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddHttpClientInstrumentation();

        if (observabilityConsoleExporter)
        {
            tracing.AddConsoleExporter();
        }

        if (observabilityOtlpEndpoint is not null)
        {
            tracing.AddOtlpExporter(options => options.Endpoint = observabilityOtlpEndpoint);
        }
    })
    .WithMetrics(metrics =>
    {
        metrics.AddMeter(BusinessMetrics.MeterName);
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddHttpClientInstrumentation();
        metrics.AddRuntimeInstrumentation();

        if (observabilityConsoleExporter)
        {
            metrics.AddConsoleExporter();
        }

        if (observabilityOtlpEndpoint is not null)
        {
            metrics.AddOtlpExporter(options => options.Endpoint = observabilityOtlpEndpoint);
        }

        if (observabilityPrometheusExporter)
        {
            metrics.AddPrometheusExporter();
        }
    });
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IMaintenanceState, MaintenanceState>();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<AuthSecurityOptions>(builder.Configuration.GetSection("AuthSecurity"));
builder.Services.Configure<ZaloOptions>(builder.Configuration.GetSection("Zalo"));
builder.Services.Configure<ReminderSchedulerOptions>(builder.Configuration.GetSection("Reminders"));
builder.Services.Configure<ReportScheduleWorkerOptions>(builder.Configuration.GetSection("ReportSchedules"));
builder.Services.Configure<ReportDeliveryEmailOptions>(builder.Configuration.GetSection("ReportDeliveryEmail"));
builder.Services.Configure<InvoiceCreditReconcileOptions>(builder.Configuration.GetSection("InvoiceReconcile"));
builder.Services.Configure<CustomerBalanceReconcileOptions>(builder.Configuration.GetSection("CustomerBalanceReconcile"));
builder.Services.Configure<DataRetentionOptions>(builder.Configuration.GetSection("DataRetention"));
builder.Services.Configure<RiskModelTrainingOptions>(builder.Configuration.GetSection("RiskModelTraining"));
builder.Services.Configure<RiskDeltaWorkerOptions>(builder.Configuration.GetSection("RiskDelta"));
builder.Services.Configure<ErpIntegrationOptions>(builder.Configuration.GetSection("ErpIntegration"));
builder.Services.AddHostedService<ReminderHostedService>();
builder.Services.AddHostedService<ReportScheduleHostedService>();
builder.Services.AddHostedService<InvoiceCreditReconcileHostedService>();
builder.Services.AddHostedService<CustomerBalanceReconcileHostedService>();
builder.Services.AddHostedService<DataRetentionHostedService>();
builder.Services.AddHostedService<RiskModelTrainingHostedService>();
builder.Services.AddHostedService<RiskDeltaHostedService>();
builder.Services.AddHostedService<BackupSchedulerHostedService>();
builder.Services.AddHostedService<BackupWorkerHostedService>();
builder.Services.AddHostedService<MaintenanceJobWorkerHostedService>();
var configuredCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var allowedCorsOrigins = configuredCorsOrigins
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim())
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ApiCors", policy =>
    {
        if (allowedCorsOrigins.Length > 0)
        {
            policy.WithOrigins(allowedCorsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
            return;
        }

        if (builder.Environment.IsDevelopment())
        {
            policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
            return;
        }

        policy.SetIsOriginAllowed(_ => false);
    });
});

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
AuthSecurityPolicy.ValidateJwtOptions(jwtOptions, builder.Environment.IsDevelopment());

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(AuthSecurityPolicy.LoginRateLimiterPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: AuthSecurityPolicy.ResolveClientPartitionKey(context),
            factory: static _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy(AuthSecurityPolicy.RefreshRateLimiterPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: AuthSecurityPolicy.ResolveClientPartitionKey(context),
            factory: static _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ImportUpload", policy => policy.RequireRole("Admin", "Supervisor", "Accountant"));
    options.AddPolicy("ImportCommit", policy => policy.RequireRole("Admin", "Supervisor"));
    options.AddPolicy("ImportHistory", policy => policy.RequireRole("Admin", "Supervisor", "Accountant"));
    options.AddPolicy("AdvanceManage", policy => policy.RequireRole("Admin", "Supervisor", "Accountant"));
    options.AddPolicy("ReceiptApprove", policy => policy.RequireRole("Admin", "Supervisor", "Accountant"));
    options.AddPolicy("PeriodLockManage", policy => policy.RequireRole("Admin", "Supervisor"));
    options.AddPolicy("ReportsView", policy => policy.RequireRole("Admin", "Supervisor", "Accountant", "Viewer"));
    options.AddPolicy("CustomerView", policy => policy.RequireRole("Admin", "Supervisor", "Accountant", "Viewer"));
    options.AddPolicy("CustomerManage", policy => policy.RequireRole("Admin", "Supervisor"));
    options.AddPolicy("InvoiceManage", policy => policy.RequireRole("Admin", "Supervisor"));
    options.AddPolicy("AdminManage", policy => policy.RequireRole("Admin"));
    options.AddPolicy("AuditView", policy => policy.RequireRole("Admin", "Supervisor"));
    options.AddPolicy("AdminHealthView", policy => policy.RequireRole("Admin", "Supervisor"));
    options.AddPolicy("RiskView", policy => policy.RequireRole("Admin", "Supervisor", "Accountant", "Viewer"));
    options.AddPolicy("RiskManage", policy => policy.RequireRole("Admin", "Supervisor"));
    options.AddPolicy("BackupManage", policy => policy.RequireRole("Admin", "Supervisor"));
    options.AddPolicy("BackupRestore", policy => policy.RequireRole("Admin"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment() && allowedCorsOrigins.Length == 0)
{
    app.Logger.LogWarning("Cors:AllowedOrigins is empty. Cross-origin browser requests are blocked.");
}

app.UseCors("ApiCors");
app.UseMiddleware<ApiVersionCompatibilityMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<MaintenanceMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ReadModelCacheInvalidationMiddleware>();

if (observabilityPrometheusExporter)
{
    app.MapPrometheusScrapingEndpoint(observabilityPrometheusPath);
}

MigrationRunner.ApplyMigrations(app.Configuration, app.Logger);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ConGNoDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");
    try
    {
        await SeedData.SeedAsync(db, app.Configuration, CancellationToken.None);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "SeedData failed.");
    }
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("Health");

app.MapGet("/health/ready", async (
        ConGNoDbContext db,
        IOptions<ReminderSchedulerOptions> reminderOptions,
        IOptions<ReportScheduleWorkerOptions> reportScheduleOptions,
        IOptions<InvoiceCreditReconcileOptions> invoiceReconcileOptions,
        IOptions<CustomerBalanceReconcileOptions> customerBalanceReconcileOptions,
        IOptions<DataRetentionOptions> dataRetentionOptions,
        IOptions<RiskModelTrainingOptions> riskModelTrainingOptions,
        IOptions<RiskDeltaWorkerOptions> riskDeltaOptions,
        IOptions<ZaloOptions> zaloOptions,
        IOptions<ErpIntegrationOptions> erpOptions,
        ZaloCircuitBreaker zaloCircuitBreaker,
        CancellationToken ct) =>
    {
        var canConnect = await db.Database.CanConnectAsync(ct);
        var dependencyFailures = new List<string>();

        if (!canConnect)
        {
            dependencyFailures.Add("database");
        }

        var zalo = zaloOptions.Value;
        var zaloConfigured = !zalo.Enabled ||
            (!string.IsNullOrWhiteSpace(zalo.ApiBaseUrl) && !string.IsNullOrWhiteSpace(zalo.AccessToken));
        var zaloCircuitOpen = false;
        TimeSpan zaloRetryAfter;
        if (zalo.Enabled)
        {
            var canSend = zaloCircuitBreaker.CanExecute(DateTimeOffset.UtcNow, out zaloRetryAfter);
            zaloCircuitOpen = !canSend;
        }
        else
        {
            zaloRetryAfter = TimeSpan.Zero;
        }

        if (zalo.Enabled && !zaloConfigured)
        {
            dependencyFailures.Add("zalo");
        }

        var erp = erpOptions.Value;
        var erpConfigured = !erp.Enabled ||
            (!string.IsNullOrWhiteSpace(erp.BaseUrl)
                && !string.IsNullOrWhiteSpace(erp.ApiKey)
                && !string.IsNullOrWhiteSpace(erp.CompanyCode));
        if (erp.Enabled && !erpConfigured)
        {
            dependencyFailures.Add("erpIntegration");
        }

        var checks = new Dictionary<string, object?>
        {
            ["database"] = canConnect ? "ok" : "unavailable",
            ["reminderWorker"] = reminderOptions.Value.AutoRunEnabled ? "enabled" : "disabled",
            ["reportScheduleWorker"] = reportScheduleOptions.Value.AutoRunEnabled ? "enabled" : "disabled",
            ["invoiceReconcileWorker"] = invoiceReconcileOptions.Value.AutoRunEnabled ? "enabled" : "disabled",
            ["customerBalanceReconcileWorker"] = customerBalanceReconcileOptions.Value.AutoRunEnabled ? "enabled" : "disabled",
            ["dataRetentionWorker"] = dataRetentionOptions.Value.AutoRunEnabled ? "enabled" : "disabled",
            ["riskModelWorker"] = riskModelTrainingOptions.Value.AutoRunEnabled ? "enabled" : "disabled",
            ["riskDeltaWorker"] = riskDeltaOptions.Value.AutoRunEnabled ? "enabled" : "disabled",
            ["zalo"] = new
            {
                enabled = zalo.Enabled,
                configured = zaloConfigured,
                circuit = zalo.Enabled
                    ? (zaloCircuitOpen ? "open" : "closed")
                    : "disabled",
                retryAfterMs = zaloCircuitOpen ? (int)zaloRetryAfter.TotalMilliseconds : 0
            },
            ["erpIntegration"] = new
            {
                enabled = erp.Enabled,
                configured = erpConfigured,
                provider = erp.Provider
            }
        };

        return dependencyFailures.Count == 0
            ? Results.Ok(new { status = "ok", checks })
            : Results.Problem(
                title: "Dependency unavailable",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "DEPENDENCY_UNAVAILABLE",
                    ["failures"] = dependencyFailures,
                    ["checks"] = checks
                });
    })
    .WithName("HealthReady");

app.MapAuthEndpoints();
app.MapImportEndpoints();
app.MapAdvanceEndpoints();
app.MapReceiptEndpoints();
app.MapPeriodLockEndpoints();
app.MapReportEndpoints();
app.MapDashboardEndpoints();
app.MapCustomerEndpoints();
app.MapAdminEndpoints();
app.MapAdminMaintenanceEndpoints();
app.MapLookupEndpoints();
app.MapSearchEndpoints();
app.MapInvoiceEndpoints();
app.MapRiskEndpoints();
app.MapCollectionEndpoints();
app.MapReminderEndpoints();
app.MapNotificationEndpoints();
app.MapZaloEndpoints();
app.MapErpIntegrationEndpoints();
app.MapBackupEndpoints();

app.Run();
