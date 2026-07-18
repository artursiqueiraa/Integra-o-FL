using CentralHub.SDK.Jfl.Server;
using Microsoft.Extensions.Logging.Abstractions;

namespace CentralHub.SDK.Tests.Server;

public class SessionManagerTests
{
    private static JflSession NovaSessao(string? numeroSerie = "1234567890")
    {
        var session = new JflSession(new MemoryStream(), "127.0.0.1:12345")
        {
            NumeroSerie = numeroSerie,
        };
        return session;
    }

    [Fact]
    public void Registrar_deve_permitir_localizar_a_sessao_por_TryGet()
    {
        var manager = new SessionManager(NullLogger<SessionManager>.Instance);
        var session = NovaSessao();

        manager.Registrar(session);

        Assert.True(manager.TryGet("1234567890", out var encontrada));
        Assert.Same(session, encontrada);
        Assert.Equal(1, manager.QuantidadeAtiva);
    }

    [Fact]
    public void Registrar_sem_numero_de_serie_deve_lancar()
    {
        var manager = new SessionManager(NullLogger<SessionManager>.Instance);
        var session = NovaSessao(numeroSerie: null);

        Assert.Throws<InvalidOperationException>(() => manager.Registrar(session));
    }

    [Fact]
    public void Registrar_deve_disparar_evento_SessaoRegistrada()
    {
        var manager = new SessionManager(NullLogger<SessionManager>.Instance);
        JflSession? recebida = null;
        manager.SessaoRegistrada += s => recebida = s;

        var session = NovaSessao();
        manager.Registrar(session);

        Assert.Same(session, recebida);
    }

    [Fact]
    public void Registrar_com_mesmo_numero_de_serie_deve_substituir_e_encerrar_a_sessao_anterior()
    {
        var manager = new SessionManager(NullLogger<SessionManager>.Instance);
        var removidas = new List<JflSession>();
        manager.SessaoRemovida += s => removidas.Add(s);

        var sessaoAntiga = NovaSessao();
        var sessaoNova = NovaSessao();

        manager.Registrar(sessaoAntiga);
        manager.Registrar(sessaoNova);

        Assert.True(manager.TryGet("1234567890", out var atual));
        Assert.Same(sessaoNova, atual);
        Assert.Equal(JflSessionState.Encerrada, sessaoAntiga.State);
        Assert.Contains(sessaoAntiga, removidas);
        Assert.Equal(1, manager.QuantidadeAtiva);
    }

    [Fact]
    public void Remover_deve_disparar_SessaoRemovida_e_limpar_o_registro()
    {
        var manager = new SessionManager(NullLogger<SessionManager>.Instance);
        var removidas = new List<JflSession>();
        manager.SessaoRemovida += s => removidas.Add(s);

        var session = NovaSessao();
        manager.Registrar(session);
        manager.Remover(session);

        Assert.False(manager.TryGet("1234567890", out _));
        Assert.Single(removidas);
        Assert.Equal(0, manager.QuantidadeAtiva);
    }

    [Fact]
    public void Remover_de_uma_sessao_ja_substituida_nao_deve_afetar_a_sessao_atual()
    {
        var manager = new SessionManager(NullLogger<SessionManager>.Instance);
        var sessaoAntiga = NovaSessao();
        var sessaoNova = NovaSessao();

        manager.Registrar(sessaoAntiga);
        manager.Registrar(sessaoNova); // substitui e ja remove a antiga internamente

        manager.Remover(sessaoAntiga); // no-op: nao e mais a sessao "atual"

        Assert.True(manager.TryGet("1234567890", out var atual));
        Assert.Same(sessaoNova, atual);
    }

    [Fact]
    public void NotificarAtividade_deve_disparar_evento_apenas_para_a_sessao_registrada_atual()
    {
        var manager = new SessionManager(NullLogger<SessionManager>.Instance);
        var notificacoes = new List<JflSession>();
        manager.AtividadeAtualizada += s => notificacoes.Add(s);

        var sessaoAntiga = NovaSessao();
        manager.Registrar(sessaoAntiga);

        var sessaoNova = NovaSessao();
        manager.Registrar(sessaoNova); // sessaoAntiga deixa de ser a atual

        manager.NotificarAtividade(sessaoAntiga); // nao deve disparar nada
        manager.NotificarAtividade(sessaoNova); // deve disparar

        Assert.Single(notificacoes);
        Assert.Same(sessaoNova, notificacoes[0]);
    }
}
