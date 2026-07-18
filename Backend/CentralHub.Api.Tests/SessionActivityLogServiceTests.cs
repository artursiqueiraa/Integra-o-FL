using CentralHub.Api.Logging;
using CentralHub.SDK.Jfl.Server;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CentralHub.Api.Tests;

public class SessionActivityLogServiceTests
{
    private static SessionManager NovoSessionManager() => new(NullLogger<SessionManager>.Instance);

    [Fact]
    public void Registrar_com_NumeroSerie_direto_deve_manter_como_esta()
    {
        var service = new SessionActivityLogService(NovoSessionManager());

        service.Registrar(new AtividadeLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Nivel = LogLevel.Information,
            Categoria = "Teste",
            Mensagem = "msg",
            NumeroSerie = "1234567890",
        });

        var resultado = service.ObterPara("1234567890");
        Assert.Single(resultado);
    }

    [Fact]
    public async Task Registrar_sem_NumeroSerie_deve_resolver_via_RemoteEndPoint_apos_SessaoRegistrada()
    {
        var sessionManager = NovoSessionManager();
        var service = new SessionActivityLogService(sessionManager);
        await service.StartAsync(CancellationToken.None);

        var stream = new MemoryStream();
        var sessao = new JflSession(stream, "127.0.0.1:5555") { NumeroSerie = "1111111111" };
        sessionManager.Registrar(sessao);

        service.Registrar(new AtividadeLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Nivel = LogLevel.Debug,
            Categoria = "Teste",
            Mensagem = "pacote recebido",
            RemoteEndPoint = "127.0.0.1:5555",
        });

        var resultado = service.ObterPara("1111111111");
        Assert.Single(resultado);
        Assert.Equal("pacote recebido", resultado[0].Mensagem);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void ObterPara_deve_devolver_mais_recente_primeiro_e_respeitar_max()
    {
        var service = new SessionActivityLogService(NovoSessionManager());

        for (var i = 0; i < 5; i++)
        {
            service.Registrar(new AtividadeLogEntry
            {
                Timestamp = DateTimeOffset.UtcNow.AddSeconds(i),
                Nivel = LogLevel.Information,
                Categoria = "Teste",
                Mensagem = $"entrada {i}",
                NumeroSerie = "2222222222",
            });
        }

        var resultado = service.ObterPara("2222222222", max: 2);

        Assert.Equal(2, resultado.Count);
        Assert.Equal("entrada 4", resultado[0].Mensagem);
        Assert.Equal("entrada 3", resultado[1].Mensagem);
    }

    [Fact]
    public void ObterPara_de_numero_de_serie_sem_entradas_deve_devolver_lista_vazia()
    {
        var service = new SessionActivityLogService(NovoSessionManager());

        var resultado = service.ObterPara("0000000000");

        Assert.Empty(resultado);
    }
}
