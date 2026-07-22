using CentralHub.Api.Data;
using CentralHub.Api.Logging;
using CentralHub.Api.Middleware;
using CentralHub.Api.Services;
using CentralHub.SDK.Jfl;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CentralHub API",
        Version = "v1",
        Description = "API para cadastro de Prédios, Centrais de alarme e envio de comandos de PGM (Pulso, Ligar, Desligar)."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<BuildingService>();
builder.Services.AddScoped<CentralService>();
builder.Services.AddScoped<CentralStatusService>();
builder.Services.AddScoped<PgmService>();
builder.Services.AddScoped<ArmService>();
builder.Services.AddScoped<ZoneInhibitService>();
builder.Services.AddScoped<CentralSessionService>();
builder.Services.AddScoped<PgmPredioService>();
builder.Services.AddScoped<ZonaPredioService>();

// Captura de atividade da sessao (painel "Log da Central", SEQ/bytes/latencia/ultimo comando)
// — so observa logs estruturados que o SDK ja emite hoje (appsettings.json ja tem
// Logging:LogLevel:CentralHub.SDK.Jfl=Debug); nunca altera nem intercepta nada do SDK.
builder.Services.AddSingleton<SessionActivityLogService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SessionActivityLogService>());
// Resolve SessionActivityLogService de forma preguicosa (passa o IServiceProvider, nao a
// instancia) para nao criar dependencia circular com SessionManager/ILoggerFactory — ver
// comentario em SdkActivityLoggerProvider.cs.
builder.Services.AddSingleton<ILoggerProvider>(sp => new SdkActivityLoggerProvider(sp));

// KeepAliveService (legado) foi desativado: ele sobrescrevia Central.Status/Latencia
// a cada 30s com base em TcpClient de saida para Central.IP:Central.Porta, o que
// conflita diretamente com o Status/UltimoKeepAliveEmUtc agora mantidos pela sessao
// TCP real via JflSessionPersistenceService. A classe continua no projeto (nao foi
// removida), apenas parou de ser registrada.
// builder.Services.AddHostedService<KeepAliveService>();

// Servidor TCP JFL (recebe conexoes iniciadas pelas centrais, conforme o protocolo
// oficial) — infraestrutura de base: apenas conexao (0x21) e keep-alive (0x40) sao
// tratados de fato; os demais comandos permanecem como stubs (ver SDK/Jfl/Server/Handlers/Stubs).
builder.Services.AddJflServer(options =>
{
    options.Porta = builder.Configuration.GetValue("Jfl:Porta", 8085);
    options.IntervaloKeepAliveMinutos = (byte)builder.Configuration.GetValue("Jfl:IntervaloKeepAliveMinutos", 5);
    options.LogHexAtivado = builder.Configuration.GetValue("Jfl:LogHexAtivado", false);
});
builder.Services.AddHostedService<JflServerHostedService>();
builder.Services.AddHostedService<JflSessionPersistenceService>();

// Origens permitidas vêm de appsettings.{Environment}.json ("Cors:AllowedOrigins"), nunca
// hardcoded aqui — assim Development (porta do Vite pode variar: 5173, 5174, ...) e Production
// (domínio real) ficam configurados separadamente, sem risco de um vazar para o outro.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();
}

app.UseGlobalExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CentralHub API v1");
    });
}

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
