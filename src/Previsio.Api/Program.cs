using System.Text;
using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Previsio.Api.Autenticacao;
using Previsio.Api.Avisos;
using Previsio.Api.Exportacao;
using Previsio.Api.NotasSoPdf;
using Previsio.Api.Resumos;
using Previsio.Api.Saude;
using Previsio.Api.Sefaz;
using Previsio.Api.Email;
using Previsio.Api.Jobs;
using Previsio.Api.Servicos;
using Previsio.Bling;
using Previsio.Core.Entidades;
using Previsio.Core.NFe;
using Previsio.Core.Painel;
using Previsio.Data;
using Previsio.Data.Repositorios;

var builder = WebApplication.CreateBuilder(args);

string conexao = builder.Configuration.GetConnectionString("Previsio")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:Previsio no appsettings.");

// Banco
builder.Services.AddDbContext<PrevisioDbContext>(opcoes => opcoes.UseNpgsql(conexao));
builder.Services.AddScoped(typeof(IRepositorio<>), typeof(Repositorio<>));

// Criptografia de tokens e senhas. As chaves ficam numa pasta fixa:
// se ela for apagada, os tokens gravados não poderão mais ser lidos.
string pastaChaves = builder.Configuration["DataProtection:PastaChaves"] ?? "chaves-dataprotection";
builder.Services.AddDataProtection()
    .SetApplicationName("Previsio")
    .PersistKeysToFileSystem(new DirectoryInfo(pastaChaves));

// Bling
builder.Services.Configure<BlingOptions>(builder.Configuration.GetSection(BlingOptions.Secao));
builder.Services.AddSingleton<LimitadorRequisicoes>();
builder.Services.AddHttpClient<BlingClient>(cliente =>
{
    cliente.Timeout = TimeSpan.FromSeconds(60);
});

// Serviços do Previsio
builder.Services.AddSingleton<LeitorNFe>();
builder.Services.AddSingleton<CalculadoraPainel>();
builder.Services.AddSingleton<ProtetorSegredos>();
builder.Services.AddScoped<TokenBlingService>();
builder.Services.AddScoped<RegistroNotaService>();
builder.Services.AddScoped<AcessoEmpresaService>();
builder.Services.AddScoped<ClientesService>();
builder.Services.AddScoped<EmpresasService>();
builder.Services.AddScoped<UsuariosService>();
builder.Services.AddScoped<PainelService>();
builder.Services.AddScoped<NotasService>();
builder.Services.AddScoped<FalhasService>();
builder.Services.AddScoped<BlingConexaoService>();
builder.Services.AddScoped<IConexaoImap, ConexaoImapMailKit>();
builder.Services.AddScoped<TesteEmailService>();
builder.Services.AddScoped<LeituraEmailJob>();
builder.Services.AddScoped<LancamentoNotaJob>();
builder.Services.AddScoped<SincronizacaoContasJob>();

// Autenticação: token JWT no header "Authorization: Bearer {token}"
IConfigurationSection secaoJwt = builder.Configuration.GetSection(JwtOptions.Secao);
JwtOptions opcoesJwt = secaoJwt.Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(opcoesJwt.Chave) < JwtOptions.TamanhoMinimoChave)
{
    throw new InvalidOperationException(
        "Configure Jwt:Chave com pelo menos " + JwtOptions.TamanhoMinimoChave + " caracteres, fora do Git.");
}
if (opcoesJwt.ValidadeMinutos <= 0)
{
    throw new InvalidOperationException("Jwt:ValidadeMinutos precisa ser maior que zero.");
}

builder.Services.Configure<JwtOptions>(secaoJwt);
builder.Services.AddSingleton<GeradorTokenJwt>();
builder.Services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.AddScoped<AutenticacaoService>();
builder.Services.AddScoped<RedefinicaoSenhaService>();
builder.Services.AddScoped<AvisosService>();
builder.Services.AddScoped<AvisosJob>();
builder.Services.AddScoped<ResumoSemanalService>();
builder.Services.AddScoped<ResumoSemanalJob>();
builder.Services.AddScoped<SaudeService>();
builder.Services.AddScoped<ExportacaoService>();

// Notas que chegam só em PDF: certificado A1 por empresa e busca do XML na SEFAZ
builder.Services.Configure<SefazOptions>(builder.Configuration.GetSection(SefazOptions.Secao));
builder.Services.AddScoped<ISefaz, SefazSoap>();
builder.Services.AddScoped<CertificadosService>();
builder.Services.AddScoped<RegistroNotaSoPdf>();
builder.Services.AddScoped<NotasSoPdfService>();
builder.Services.AddScoped<BuscaXmlSefazService>();
builder.Services.AddScoped<BuscaXmlSefazJob>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.Secao));
builder.Services.AddSingleton<IEnvioEmail, EnvioEmailSmtp>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opcoes =>
    {
        // Mantém os nomes curtos das claims ("sub", "role", "cliente_id")
        opcoes.MapInboundClaims = false;
        opcoes.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = opcoesJwt.Emissor,
            ValidateAudience = true,
            ValidAudience = opcoesJwt.Audiencia,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = GeradorTokenJwt.CriarChave(opcoesJwt.Chave),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = ClaimsPrevisio.Nome,
            RoleClaimType = ClaimsPrevisio.Perfil
        };
        opcoes.Events = new JwtBearerEvents
        {
            OnTokenValidated = ValidacaoTokenUsuario.ValidarAsync
        };
    });

builder.Services.AddAuthorization(opcoes =>
{
    // Toda rota exige login, a não ser que esteja marcada com [AllowAnonymous]
    opcoes.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

int tentativasLogin = builder.Configuration.GetValue<int>(
    LimitesRequisicao.ChaveTentativasLogin, LimitesRequisicao.TentativasLoginPadrao);
if (tentativasLogin <= 0)
{
    throw new InvalidOperationException(LimitesRequisicao.ChaveTentativasLogin + " precisa ser maior que zero.");
}

builder.Services.AddRateLimiter(opcoes =>
{
    opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opcoes.AddPolicy(LimitesRequisicao.Login, contexto =>
    {
        string ip = "desconhecido";
        if (contexto.Connection.RemoteIpAddress != null)
        {
            ip = contexto.Connection.RemoteIpAddress.ToString();
        }

        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = tentativasLogin,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

// Jobs em segundo plano
builder.Services.AddHangfire(configuracao => configuracao
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(opcoes => opcoes.UseNpgsqlConnection(conexao)));
builder.Services.AddHangfireServer(opcoes =>
{
    opcoes.WorkerCount = 2;
});
builder.Services.AddSingleton<IExecucaoJobs, ExecucaoJobsHangfire>();
builder.Services.AddSingleton<IAgendaJobs, AgendaJobsHangfire>();

// Painel Angular
string urlFront = builder.Configuration["Front:Url"] ?? "http://localhost:4200";
Uri? enderecoFront;
if (!Uri.TryCreate(urlFront, UriKind.Absolute, out enderecoFront))
{
    throw new InvalidOperationException("Front:Url precisa ser uma URL completa, como http://localhost:4200/#.");
}
// O navegador envia só a origem (esquema, host e porta): o "/#" das rotas do painel fica de fora
string origemFront = enderecoFront.GetLeftPart(UriPartial.Authority);
builder.Services.AddCors(opcoes =>
{
    opcoes.AddPolicy("front", politica => politica
        .WithOrigins(origemFront)
        .AllowAnyHeader()
        .AllowAnyMethod()
        // Nome do arquivo das exportações (o front usa para salvar)
        .WithExposedHeaders("Content-Disposition"));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opcoes =>
{
    // Botão "Authorize" do Swagger: cole o token retornado por POST /api/auth/login
    var esquema = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Token retornado por POST /api/auth/login"
    };
    opcoes.AddSecurityDefinition("Bearer", esquema);

    var referencia = new OpenApiSecurityScheme
    {
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    var requisito = new OpenApiSecurityRequirement();
    requisito.Add(referencia, new List<string>());
    opcoes.AddSecurityRequirement(requisito);
});

var app = builder.Build();

// Aplica as migrations ao subir (prático no piloto)
using (IServiceScope escopo = app.Services.CreateScope())
{
    PrevisioDbContext db = escopo.ServiceProvider.GetRequiredService<PrevisioDbContext>();
    db.Database.Migrate();

    // Primeiro administrador, a partir da seção AdminInicial (só se ainda não houver nenhum)
    AdminInicial.Garantir(
        db,
        escopo.ServiceProvider.GetRequiredService<AutenticacaoService>(),
        app.Configuration,
        app.Logger);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("front");

// Por padrão o painel do Hangfire só abre a partir da própria máquina.
// Fica antes da autenticação porque o navegador não envia o token JWT.
app.UseHangfireDashboard("/hangfire");

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Agenda dos jobs
IRecurringJobManager agenda = app.Services.GetRequiredService<IRecurringJobManager>();
var opcoesAgenda = new RecurringJobOptions { TimeZone = RelogioBrasil.Fuso };

agenda.AddOrUpdate<LeituraEmailJob>(
    "leitura-email", j => j.ExecutarAsync(CancellationToken.None), "*/10 * * * *", opcoesAgenda);
agenda.AddOrUpdate<LancamentoNotaJob>(
    "lancamento", j => j.LancarPendentesAsync(CancellationToken.None), "*/10 * * * *", opcoesAgenda);
// 5 minutos depois da leitura e do lançamento: avisos do que precisa de alguém e o e-mail dos novos
agenda.AddOrUpdate<AvisosJob>(
    "avisos", j => j.ExecutarAsync(CancellationToken.None), "5-59/10 * * * *", opcoesAgenda);
agenda.AddOrUpdate<SincronizacaoContasJob>(
    "sincronizacao", j => j.ExecutarAsync(CancellationToken.None), "*/30 * * * *", opcoesAgenda);
// De 5 em 5 minutos: o XML das notas que chegaram só em PDF, na SEFAZ
agenda.AddOrUpdate<BuscaXmlSefazJob>(
    "busca-xml-sefaz", j => j.ExecutarAsync(CancellationToken.None), "2-59/5 * * * *", opcoesAgenda);
// De hora em hora: o resumo semanal das redes cujo dia e hora chegaram (cada rede escolhe os seus)
agenda.AddOrUpdate<ResumoSemanalJob>(
    "resumo-semanal", j => j.ExecutarAsync(CancellationToken.None), "2 * * * *", opcoesAgenda);

app.Run();
