namespace CentralHub.SDK.Jfl.Protocol;

/// <summary>
/// Bytes de comando (campo CMD) do protocolo 0x7B, conforme documentado para a
/// Active 100 Bus. Nomeados por secao do documento oficial.
/// </summary>
public enum JflCommand : byte
{
    /// <summary>3.1 - Comando de conexao (mandatorio). Enviado pelo equipamento ao abrir o socket.</summary>
    Conexao = 0x21,

    /// <summary>3.1 - Comando de conexao para os modulos M-300+/M-300 Flex (mesma estrutura do 0x21).</summary>
    ConexaoModulo = 0x2A,

    /// <summary>3.2 - Comando de pedir status (opcional).</summary>
    PedirStatus = 0x93,

    /// <summary>3.3 - Comando de keep alive (mandatorio).</summary>
    KeepAlive = 0x40,

    /// <summary>3.4 - Comando de evento (mandatorio). Enviado pelo equipamento a qualquer momento.</summary>
    Evento = 0x24,

    /// <summary>4.1 - Comando de status como superusuario (tela monitorar).</summary>
    Status = 0x4D,

    /// <summary>4.2 - Comando de armar como superusuario.</summary>
    Armar = 0x4E,

    /// <summary>4.3 - Comando de desarmar como superusuario.</summary>
    Desarmar = 0x4F,

    /// <summary>4.4 - Comando de acionar PGM como superusuario.</summary>
    AcionarPgm = 0x50,

    /// <summary>4.5 - Comando de desacionar PGM como superusuario.</summary>
    DesacionarPgm = 0x51,

    /// <summary>4.6 - Comando de inibir zonas como superusuario.</summary>
    InibirZonas = 0x52,

    /// <summary>4.7 - Comando de armar STAY como superusuario.</summary>
    ArmarStay = 0x53,

    /// <summary>4.8 - Comando de armar AWAY como superusuario.</summary>
    ArmarAway = 0x54,

    /// <summary>4.9 - Comando de atualizar data e hora da central.</summary>
    AtualizarDataHora = 0x55,

    /// <summary>
    /// 5.x - Envelope dos comandos remotos com senha da linha Active (armar/desarmar,
    /// inibir/desinibir zonas, PGM, consulta e programacao de usuario). O sub-comando
    /// real (0xC1, 0xC3, 0xC7, 0xC8, 0xC9, 0xCF...) vem no primeiro byte de Dados.
    /// </summary>
    ComSenha = 0x37,
}
