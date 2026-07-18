namespace CentralHub.SDK.Jfl.Messages;

/// <summary>Campo RESULT da resposta do comando de conexao (secao 3.1).</summary>
public enum ConnectionResult : byte
{
    /// <summary>Numero de serie bloqueado — o equipamento derruba a conexao ao receber este resultado.</summary>
    Bloqueado = 0x00,

    /// <summary>Numero de serie liberado — o equipamento passa a operar normalmente (keep-alive, eventos).</summary>
    Liberado = 0x01,
}

/// <summary>Payload (campo Dados) da resposta ao comando de conexao: RESULT(1) + KEEP(1).</summary>
public sealed class ConnectionResponse
{
    public required ConnectionResult Resultado { get; init; }

    /// <summary>Intervalo de keep-alive em minutos (1 a 20; fora da faixa, o equipamento assume 1 minuto).</summary>
    public required byte IntervaloKeepAliveMinutos { get; init; }

    public byte[] ToDados() => [(byte)Resultado, IntervaloKeepAliveMinutos];
}
