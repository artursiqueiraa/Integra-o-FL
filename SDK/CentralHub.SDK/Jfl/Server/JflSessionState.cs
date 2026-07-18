namespace CentralHub.SDK.Jfl.Server;

public enum JflSessionState
{
    /// <summary>Socket aberto, aguardando o comando de conexao (0x21/0x2A) do equipamento.</summary>
    Conectando,

    /// <summary>Comando de conexao processado e aceito; sessao registrada no <see cref="SessionManager"/>.</summary>
    Ativa,

    /// <summary>Sessao encerrada (conexao caiu, foi bloqueada, ou foi substituida por uma reconexao).</summary>
    Encerrada,
}
