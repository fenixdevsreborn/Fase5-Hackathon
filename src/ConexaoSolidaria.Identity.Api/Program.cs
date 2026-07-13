using System.Text;
using Asp.Versioning;
using ConexaoSolidaria.Identity.Api.Data;
using ConexaoSolidaria.Identity.Api.Security;
using ConexaoSolidaria.Contracts.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("IdentityDb"),
        npgsql => npgsql.EnableRetryOnFailure()));

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddProblemDetails();

// B6 - Versionamento de API por header (x-api-version) e/ou query (api-version).
// NAO usa segmento de URL para nao quebrar /api/auth/*, o Gateway e os testes de integracao.
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = ApiVersionReader.Combine(
            new HeaderApiVersionReader("x-api-version"),
            new QueryStringApiVersionReader("api-version"));
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Conexao Solidaria - Identity API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Informe o token JWT no formato: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CampaignManagement", policy => policy.RequireRole(ApplicationRoles.GestorOng))
    .AddPolicy("DonationCreation", policy => policy.RequireRole(ApplicationRoles.Doador));

builder.Services.AddHealthChecks();

var app = builder.Build();

// B8 - Job de migracao do k8s: RunMigrationsOnly=true executa as migrations e encerra o processo.
if (app.Configuration.GetValue<bool>("RunMigrationsOnly"))
{
    await IdentityDatabaseInitializer.MigrateOnlyAsync(app.Services);
    return;
}

await IdentityDatabaseInitializer.InitializeAsync(app.Services, app.Configuration);

app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpMetrics();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// /health e /alive vem do ServiceDefaults (MapDefaultEndpoints); /metrics segue no prometheus-net.
app.MapDefaultEndpoints();
app.MapMetrics();

app.Run();


public partial class Program;
