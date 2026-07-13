using ConexaoSolidaria.Contracts.Messaging;
using ConexaoSolidaria.Web.Components;
using ConexaoSolidaria.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Aspire: service discovery, resilience, health checks, OpenTelemetry.
builder.AddServiceDefaults();

// MudBlazor (dialog/snackbar/popover/resize services etc.).
builder.Services.AddMudServices();

// Blazor Web App - Interactive Server.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Armazenamento protegido do JWT (lado servidor, por circuito).
// Data Protection keys persistidas para funcionar em multi-replica (k8s):
// tokens cifrados por uma replica precisam ser decifrados por qualquer outra.
// Em producao, KeysPath deve apontar para um volume compartilhado/PVC montado
// em todas as replicas do web. Em dev, cai para uma pasta local (./keys).
var keysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "keys");
Directory.CreateDirectory(keysPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("ConexaoSolidaria");
builder.Services.AddScoped<ProtectedLocalStorage>();

// Autenticacao/estado do usuario.
builder.Services.AddScoped<TokenProvider>();
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<JwtAuthStateProvider>());
builder.Services.AddScoped<AuthService>();
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

// Cliente tipado do Gateway (base address resolvido via service discovery do Aspire).
// Resiliencia (timeout + retry em transitorios + circuit breaker) NAO e configurada
// aqui: o AddServiceDefaults ja aplica AddStandardResilienceHandler() via
// ConfigureHttpClientDefaults a TODOS os HttpClients criados pela factory, incluindo
// este ApiClient tipado. Nao duplicamos o handler para evitar retries em cascata —
// importante porque o polling de status da doacao faz chamadas repetidas.
builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri("http://gateway");
});

// Notificacoes em tempo real (best-effort) via RabbitMQ fanout -> circuito SignalR do Blazor.
// O dispatcher (singleton) reemite as notificacoes para as telas conectadas; o consumidor
// (BackgroundService) conecta ao broker com reconexao resiliente. Se o RabbitMQ estiver
// ausente/indisponivel, o app segue funcionando (o polling do DonationStatus e o fallback).
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddSingleton<NotificationDispatcher>();
builder.Services.AddHostedService<NotificationConsumer>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
