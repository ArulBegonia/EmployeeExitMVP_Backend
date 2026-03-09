using System.Text;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using FluentValidation;
using FluentValidation.AspNetCore;
using QuestPDF.Infrastructure;
using Relevantz.ExitManagement.Api.Middleware;
using Relevantz.ExitManagement.Data.DBContexts;
using Relevantz.ExitManagement.Data.Repository;
using Relevantz.ExitManagement.Data.IRepository;
using Relevantz.ExitManagement.Core.IService;
using Relevantz.ExitManagement.Core.Service;
using Relevantz.ExitManagement.Common.Validators;
using Relevantz.ExitManagement.Data;

var builder = WebApplication.CreateBuilder(args);

// ── 1) Database ──────────────────────────────────────────────────────────────
var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

builder.Services.AddDbContext<ExitManagementDbContext>(options =>
    options.UseMySql(connStr, ServerVersion.AutoDetect(connStr))
);

// ── 2) CORS ──────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

// ── 3) Controllers + JSON + FluentValidation ─────────────────────────────────
builder.Services
    .AddControllers()
    .AddJsonOptions(opt =>
        opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<SubmitResignationRequestValidator>();

// ── 4) Swagger ───────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Exit Management API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new()
    {
        Name        = "Authorization",
        Type        = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme      = "bearer",
        BearerFormat = "JWT",
        In          = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your JWT token (without 'Bearer ' prefix)"
    });

    options.AddSecurityRequirement(new()
    {
        {
            new()
            {
                Reference = new()
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ── 5) Dependency Injection ──────────────────────────────────────────────────
builder.Services.AddScoped<IExitRequestRepository, ExitRequestRepository>();
builder.Services.AddScoped<IExitService,           ExitService>();
builder.Services.AddScoped<IDocumentService,       DocumentService>();

// ── 6) JWT Authentication ────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["JwtSettings:Secret"]
    ?? throw new InvalidOperationException("JWT secret is not configured in appsettings.");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken            = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = false,
            ValidateAudience         = false,
            ValidateLifetime         = true,           // ← was missing
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew                = TimeSpan.Zero,  // ← no grace period on expiry
            RoleClaimType            = ClaimTypes.Role,
            NameClaimType            = "empId"
        };

        // Return clean JSON on 401 instead of empty body
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode  = 401;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(
                    """{"message":"Unauthorized. Please log in."}""");
            },
            OnForbidden = async ctx =>
            {
                ctx.Response.StatusCode  = 403;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(
                    """{"message":"Forbidden. You do not have permission."}""");
            }
        };
    });

// ── 7) Build App ─────────────────────────────────────────────────────────────
var app = builder.Build();

// ── 8) Seed Database ─────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ExitManagementDbContext>();
    await DbInitializer.SeedAsync(context);
}

// ── 9) Middleware Pipeline — ORDER MATTERS ────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Exit Management v1"));
}

app.UseMiddleware<GlobalExceptionMiddleware>();  // ← first: catches all unhandled exceptions

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");                   // ← before auth

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

QuestPDF.Settings.License = LicenseType.Community;

app.Run();
