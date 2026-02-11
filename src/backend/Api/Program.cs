using System.Text;
using CongNoGolden.Api.Endpoints;
using CongNoGolden.Api.Middleware;
using CongNoGolden.Api.Services;
using CongNoGolden.Application.Auth;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Services;
using CongNoGolden.Infrastructure.Migrations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration));

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IMaintenanceState, MaintenanceState>();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<ZaloOptions>(builder.Configuration.GetSection("Zalo"));
builder.Services.Configure<ReminderSchedulerOptions>(builder.Configuration.GetSection("Reminders"));
builder.Services.Configure<InvoiceCreditReconcileOptions>(builder.Configuration.GetSection("InvoiceReconcile"));
builder.Services.AddHostedService<ReminderHostedService>();
builder.Services.AddHostedService<InvoiceCreditReconcileHostedService>();
builder.Services.AddHostedService<BackupSchedulerHostedService>();
builder.Services.AddHostedService<BackupWorkerHostedService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalDev", policy =>
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

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

app.UseCors("LocalDev");
app.UseMiddleware<MaintenanceMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

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

app.MapGet("/health/ready", async (ConGNoDbContext db, CancellationToken ct) =>
    {
        var canConnect = await db.Database.CanConnectAsync(ct);
        return canConnect
            ? Results.Ok(new { status = "ok" })
            : Results.Problem(
                title: "Database unavailable",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                extensions: new Dictionary<string, object?> { ["code"] = "DEPENDENCY_UNAVAILABLE" });
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
app.MapLookupEndpoints();
app.MapInvoiceEndpoints();
app.MapRiskEndpoints();
app.MapReminderEndpoints();
app.MapNotificationEndpoints();
app.MapZaloEndpoints();
app.MapBackupEndpoints();

app.Run();
