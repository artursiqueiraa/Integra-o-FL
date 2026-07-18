using CentralHub.SDK.Jfl.Messages;
using CentralHub.SDK.Jfl.Messages.Status;
using CentralHub.SDK.Jfl.Protocol;

namespace CentralHub.SDK.Jfl.Diagnostics;

/// <summary>Um campo do pacote decomposto para exibição/depuração.</summary>
public sealed class CampoAnalisado
{
    public required string Nome { get; init; }

    public required int Offset { get; init; }

    public required int Tamanho { get; init; }

    public required string ValorBrutoHex { get; init; }

    public required string ValorInterpretado { get; init; }

    public string? Descricao { get; init; }
}

/// <summary>Resultado completo da análise de um pacote JFL 0x7B.</summary>
public sealed class PacoteAnalisado
{
    public required bool CabecalhoValido { get; init; }

    public byte? Cab { get; init; }

    public int? Qde { get; init; }

    public byte? Seq { get; init; }

    public byte? Cmd { get; init; }

    public string? CmdNome { get; init; }

    public bool? ChecksumValido { get; init; }

    public byte[]? Dados { get; init; }

    public required IReadOnlyList<CampoAnalisado> Campos { get; init; }

    /// <summary>Problemas encontrados durante a análise (checksum inválido, CMD desconhecido, pacote incompleto, etc.).</summary>
    public required IReadOnlyList<string> Avisos { get; init; }
}

/// <summary>
/// Decompõe um pacote JFL 0x7B (bruto ou em hex) em campos nomeados, para inspeção manual —
/// nunca usado no caminho real de conexão da <see cref="Server.SessionManager"/>; é uma
/// ferramenta de diagnóstico que reaproveita <see cref="PacketParser"/>/<see cref="ChecksumCalculator"/>
/// e os parsers de mensagem já existentes (<see cref="ConnectionRequest"/>,
/// <see cref="CentralStatusResponse"/>) sem alterá-los.
/// </summary>
public static class PacketAnalyzer
{
    private const int TamanhoMinimoFrame = 5; // CAB + QDE + SEQ + CMD + K

    /// <summary>
    /// Espelha o tamanho mínimo (privado) de <see cref="CentralStatusResponse"/> — soma dos
    /// campos fixos da resposta "tela monitorar" (§4.10): KP(2)+HORA(6)+BAT(1)+PGM(1)+PART(16)+
    /// ELET(1)+ZONA(50)+PROB(5)+P-ELET(1)+P-PGM(1)+P-PART(16)+P-INIB(13) = 113 bytes. Não há
    /// como reaproveitar a constante original sem alterar sua visibilidade em um arquivo
    /// homologado — mantido como duplicata documentada, só para fins de diagnóstico.
    /// </summary>
    private const int TamanhoMinimoRespostaTelaMonitorar = 113;

    /// <summary>Aceita uma string hex (com ou sem espaços/prefixo "0x") e analisa o pacote.</summary>
    public static PacoteAnalisado AnalisarHex(string hex)
    {
        var limpo = new string(hex.Where(c => Uri.IsHexDigit(c)).ToArray());

        byte[] bytes;
        try
        {
            bytes = Convert.FromHexString(limpo);
        }
        catch (FormatException ex)
        {
            return new PacoteAnalisado { CabecalhoValido = false, Campos = [], Avisos = [$"Hex inválido: {ex.Message}"] };
        }

        return Analisar(bytes);
    }

    public static PacoteAnalisado Analisar(ReadOnlySpan<byte> pacote)
    {
        var avisos = new List<string>();

        if (pacote.Length < 2)
        {
            avisos.Add("Pacote com menos de 2 bytes — não é possível ler CAB/QDE.");
            return new PacoteAnalisado { CabecalhoValido = false, Campos = [], Avisos = avisos };
        }

        var cab = pacote[0];
        if (cab != JflProtocol.Header0x7B)
        {
            avisos.Add($"Cabeçalho 0x{cab:X2} não reconhecido — esperado 0x7B (o protocolo 0x7A, de 2 bytes de QDE, não é suportado por este analisador).");
            return new PacoteAnalisado { CabecalhoValido = false, Cab = cab, Campos = [], Avisos = avisos };
        }

        var qde = pacote[1];
        if (pacote.Length < qde)
        {
            avisos.Add($"QDE declara {qde} bytes, mas só {pacote.Length} foram recebidos — pacote incompleto (mostrando o que há disponível).");
        }

        var tamanhoDisponivel = Math.Min((int)qde, pacote.Length);
        if (tamanhoDisponivel < TamanhoMinimoFrame)
        {
            avisos.Add("Pacote curto demais para conter SEQ/CMD/K (mínimo 5 bytes).");
            return new PacoteAnalisado { CabecalhoValido = true, Cab = cab, Qde = qde, Campos = [], Avisos = avisos };
        }

        bool? checksumValido = pacote.Length >= qde ? ChecksumCalculator.IsValid(pacote[..qde]) : null;
        if (checksumValido == false)
        {
            avisos.Add("Checksum inválido — o XOR de todos os bytes (incluindo K) deveria dar zero.");
        }
        else if (checksumValido is null)
        {
            avisos.Add("Não foi possível validar o checksum (pacote incompleto).");
        }

        var seq = pacote[2];
        var cmd = pacote[3];
        var dados = pacote.Slice(4, tamanhoDisponivel - TamanhoMinimoFrame).ToArray();
        var k = pacote[tamanhoDisponivel - 1];

        var cmdConhecido = Enum.IsDefined(typeof(JflCommand), cmd);
        var cmdNome = cmdConhecido ? ((JflCommand)cmd).ToString() : $"Desconhecido (0x{cmd:X2})";
        if (!cmdConhecido)
        {
            avisos.Add($"CMD 0x{cmd:X2} não está catalogado em JflCommand.");
        }

        var campos = new List<CampoAnalisado>
        {
            CampoSimples("CAB", 0, [cab], "Cabeçalho do protocolo 0x7B."),
            CampoSimples("QDE", 1, [qde], $"Tamanho total do pacote declarado: {qde} bytes."),
            CampoSimples("SEQ", 2, [seq], "Número de sequência (correlaciona pedido/resposta)."),
            CampoSimples("CMD", 3, [cmd], cmdNome),
        };

        campos.AddRange(DescreverDados((JflCommand)cmd, cmdConhecido, dados, offsetBase: 4));

        campos.Add(CampoSimples(
            "K", tamanhoDisponivel - 1, [k],
            checksumValido == true ? "Checksum OK." : "Checksum inválido ou não verificável."));

        return new PacoteAnalisado
        {
            CabecalhoValido = true,
            Cab = cab,
            Qde = qde,
            Seq = seq,
            Cmd = cmd,
            CmdNome = cmdNome,
            ChecksumValido = checksumValido,
            Dados = dados,
            Campos = campos,
            Avisos = avisos,
        };
    }

    private static CampoAnalisado CampoSimples(string nome, int offset, byte[] bytes, string descricao) => new()
    {
        Nome = nome,
        Offset = offset,
        Tamanho = bytes.Length,
        ValorBrutoHex = Convert.ToHexString(bytes),
        ValorInterpretado = bytes.Length == 1 ? $"0x{bytes[0]:X2} ({bytes[0]})" : Convert.ToHexString(bytes),
        Descricao = descricao,
    };

    /// <summary>
    /// Decompõe o campo DADOS quando o CMD é reconhecido e já existe um parser de mensagem
    /// para o formato observado — cai para um campo genérico de bytes brutos quando não há
    /// decodificação específica ainda (comandos das Fases 1-5 registram a própria decomposição
    /// quando forem implementados).
    /// </summary>
    private static List<CampoAnalisado> DescreverDados(JflCommand cmd, bool cmdConhecido, byte[] dados, int offsetBase)
    {
        if (dados.Length == 0)
        {
            return [new CampoAnalisado
            {
                Nome = "DADOS", Offset = offsetBase, Tamanho = 0, ValorBrutoHex = "", ValorInterpretado = "(vazio)",
                Descricao = "Comando sem payload — típico de um pedido (ex.: Status, KeepAlive) sem parâmetros.",
            }];
        }

        if (!cmdConhecido)
        {
            return [CampoBruto(dados, offsetBase, "CMD não catalogado — sem decodificação específica.")];
        }

        try
        {
            return cmd switch
            {
                JflCommand.Conexao or JflCommand.ConexaoModulo => DescreverConexao(dados, offsetBase),
                JflCommand.KeepAlive when dados.Length == 1 =>
                    [CampoSimples("KEEP", offsetBase, [dados[0]], $"Resposta de KeepAlive: enviar o próximo keep-alive em {DescreverIntervaloKeepAlive(dados[0])}.")],
                JflCommand.AtualizarDataHora when dados.Length == 6 => DescreverDataHora(dados, offsetBase),
                JflCommand.Armar or JflCommand.Desarmar or JflCommand.ArmarStay or JflCommand.ArmarAway when dados.Length == 1 =>
                    [CampoSimples("PART", offsetBase, [dados[0]], DescreverParticaoAlvo(dados[0]))],
                JflCommand.AcionarPgm or JflCommand.DesacionarPgm when dados.Length == 1 =>
                    [CampoSimples("PGM", offsetBase, [dados[0]], $"Número da PGM: {dados[0]}.")],
                JflCommand.InibirZonas when dados.Length == 13 => DescreverBitmapZonas(dados, offsetBase),
                _ when dados.Length >= TamanhoMinimoRespostaTelaMonitorar =>
                    DescreverRespostaTelaMonitorar(dados, offsetBase),
                _ => [CampoBruto(dados, offsetBase, "Sem decodificação específica registrada ainda para este tamanho de payload.")],
            };
        }
        catch (JflProtocolException ex)
        {
            return [CampoBruto(dados, offsetBase, $"Falha ao decodificar com o parser conhecido: {ex.Message}")];
        }
    }

    private static CampoAnalisado CampoBruto(byte[] dados, int offset, string descricao) => new()
    {
        Nome = "DADOS", Offset = offset, Tamanho = dados.Length,
        ValorBrutoHex = Convert.ToHexString(dados), ValorInterpretado = Convert.ToHexString(dados),
        Descricao = descricao,
    };

    /// <summary>1 a 20 minutos conforme enviado; fora da faixa (incl. 0x00), a central assume 1 minuto (secao 3.3).</summary>
    private static string DescreverIntervaloKeepAlive(byte keep) =>
        keep is >= 1 and <= 20 ? $"{keep} minuto(s)" : $"1 minuto (valor 0x{keep:X2} fora da faixa 1-20, assume o mínimo)";

    private static string DescreverParticaoAlvo(byte part) =>
        part == 99 ? "Partição 99 — valor especial: opera o eletrificador, não uma partição real." : $"Partição {part}.";

    private static List<CampoAnalisado> DescreverConexao(byte[] dados, int offsetBase)
    {
        // Resposta (RESULT + KEEP, 2 bytes) e pedido (>=44 bytes, ConnectionRequest) têm
        // tamanhos incompatíveis — nenhuma ambiguidade real.
        if (dados.Length == 2)
        {
            var resultado = dados[0] == 0x01 ? "Liberado" : dados[0] == 0x00 ? "Bloqueado" : $"Desconhecido (0x{dados[0]:X2})";
            return
            [
                CampoSimples("RESULT", offsetBase, [dados[0]], $"Resultado da conexão: {resultado}."),
                CampoSimples("KEEP", offsetBase + 1, [dados[1]], $"Intervalo de keep-alive: {dados[1]} minuto(s)."),
            ];
        }

        var req = ConnectionRequest.Parse(dados);
        var offset = offsetBase;
        var campos = new List<CampoAnalisado>
        {
            new() { Nome = "NS", Offset = offset, Tamanho = 10, ValorBrutoHex = Convert.ToHexString(dados.AsSpan(0, 10)), ValorInterpretado = req.NumeroSerie, Descricao = "Número de série do equipamento." },
        };
        offset += 10;
        campos.Add(new CampoAnalisado { Nome = "IMEI", Offset = offset, Tamanho = 15, ValorBrutoHex = Convert.ToHexString(dados.AsSpan(10, 15)), ValorInterpretado = req.Imei ?? "(vazio)", Descricao = "IMEI do módulo celular." });
        offset += 15;
        campos.Add(new CampoAnalisado { Nome = "MAC", Offset = offset, Tamanho = 12, ValorBrutoHex = Convert.ToHexString(dados.AsSpan(25, 12)), ValorInterpretado = req.Mac ?? "(vazio)", Descricao = "Endereço MAC." });
        offset += 12;
        campos.Add(CampoSimples("MOD", offset, [req.Modelo], $"Modelo do equipamento (byte 0x{req.Modelo:X2})."));
        offset += 1;
        campos.Add(new CampoAnalisado { Nome = "VER", Offset = offset, Tamanho = 3, ValorBrutoHex = Convert.ToHexString(dados.AsSpan(38, 3)), ValorInterpretado = req.Versao, Descricao = "Versão de firmware." });
        offset += 3;
        campos.Add(CampoSimples("IP", offset, [req.EnderecoIp], "Endereço IP configurado (1 ou 2) em uso."));
        offset += 1;
        campos.Add(CampoSimples("SIMCARD", offset, [req.SimCard], "SIM card em uso (1, 2, ou 3=não existe)."));
        offset += 1;
        campos.Add(CampoSimples("VIA", offset, [req.Via], req.Via == 0x01 ? "Ethernet." : "GPRS."));
        offset += 1;
        campos.Add(CampoSimples("OPE", offset, [req.Operadora], "Operadora de celular em uso."));
        offset += 1;
        if (req.StatusPayload.Length > 0)
        {
            campos.Add(CampoBruto(req.StatusPayload, offset, "STATUS — payload opcional (formato igual ao comando 0x93), não decodificado aqui."));
        }

        return campos;
    }

    private static List<CampoAnalisado> DescreverDataHora(byte[] dados, int offsetBase)
    {
        string[] nomes = ["HORA", "MIN", "SEG", "DIA", "MES", "ANO"];
        var campos = new List<CampoAnalisado>();
        for (var i = 0; i < 6; i++)
        {
            campos.Add(CampoSimples(nomes[i], offsetBase + i, [dados[i]], $"{JflBcdParaTexto(dados[i])} (BCD)."));
        }

        return campos;
    }

    private static string JflBcdParaTexto(byte valor) => (((valor >> 4) * 10) + (valor & 0x0F)).ToString();

    private static List<CampoAnalisado> DescreverBitmapZonas(byte[] dados, int offsetBase)
    {
        // Convencao confirmada contra capturas reais do manual (secao 4.6): bit mais
        // significativo do byte = zona menor daquele byte; bit menos significativo = zona
        // maior. Diferente do bitmap P-INIB da resposta de status (LSB-first) — ver Fase 3.
        var zonasMarcadas = new List<int>();
        for (var i = 0; i < dados.Length; i++)
        {
            for (var bit = 7; bit >= 0; bit--)
            {
                if ((dados[i] & (1 << bit)) != 0)
                {
                    var zona = (i * 8) + (7 - bit) + 1;
                    zonasMarcadas.Add(zona);
                }
            }
        }

        var resumo = zonasMarcadas.Count == 0 ? "nenhuma zona marcada" : string.Join(", ", zonasMarcadas);
        return
        [
            new CampoAnalisado
            {
                Nome = "ZONA (bitmap)", Offset = offsetBase, Tamanho = dados.Length,
                ValorBrutoHex = Convert.ToHexString(dados),
                ValorInterpretado = resumo,
                Descricao = "Comando de Inibir Zonas — bitmap MSB-first por byte (bit 7 = zona menor do byte); substitui o conjunto inteiro de zonas inibidas, não soma ao anterior.",
            },
        ];
    }

    private static List<CampoAnalisado> DescreverRespostaTelaMonitorar(byte[] dados, int offsetBase)
    {
        var status = CentralStatusResponse.Parse(dados);
        var campos = new List<CampoAnalisado>
        {
            new()
            {
                Nome = "HORA (central)", Offset = offsetBase + 2, Tamanho = 6,
                ValorBrutoHex = Convert.ToHexString(dados.AsSpan(2, 6)),
                ValorInterpretado = status.DataHoraCentral?.ToString("dd/MM/yyyy HH:mm:ss") ?? "(inválida)",
                Descricao = "Data/hora informadas pela própria central.",
            },
            new()
            {
                Nome = "BAT", Offset = offsetBase + 8, Tamanho = 1,
                ValorBrutoHex = Convert.ToHexString(dados.AsSpan(8, 1)),
                ValorInterpretado = DescreverBateria(status.Bateria),
                Descricao = "Nível de bateria.",
            },
        };

        foreach (var pgm in status.Pgms)
        {
            campos.Add(new CampoAnalisado
            {
                Nome = $"PGM {pgm.Numero}", Offset = -1, Tamanho = 0, ValorBrutoHex = "",
                ValorInterpretado = pgm.Acionada ? "Acionada" : "Desacionada",
                Descricao = pgm.Permitida ? "Usuário tem permissão nesta PGM." : "Sem permissão nesta PGM.",
            });
        }

        foreach (var particao in status.Particoes)
        {
            campos.Add(new CampoAnalisado
            {
                Nome = $"Partição {particao.Numero}", Offset = -1, Tamanho = 0, ValorBrutoHex = "",
                ValorInterpretado = particao.Desabilitada ? "Desabilitada" : particao.Estado!.Value.ToString(),
                Descricao = particao.Desabilitada ? null : $"Permite: {DescreverPermissoesParticao(particao)}.",
            });
        }

        campos.Add(new CampoAnalisado
        {
            Nome = "Eletrificador", Offset = -1, Tamanho = 0, ValorBrutoHex = "",
            ValorInterpretado = status.Eletrificador.Estado?.ToString() ?? "Desabilitado/não programado",
            Descricao = null,
        });

        var zonasRelevantes = status.Zonas.Where(z => z.Estado is not (null or ZoneState.Desabilitada)).ToList();
        campos.Add(new CampoAnalisado
        {
            Nome = "Zonas (resumo)", Offset = -1, Tamanho = 0, ValorBrutoHex = "",
            ValorInterpretado = $"{status.Zonas.Count} zonas no total, {zonasRelevantes.Count} com estado relevante (demais desabilitadas)",
            Descricao = zonasRelevantes.Count == 0 ? null : string.Join(", ", zonasRelevantes.Select(z => $"Zona {z.Numero}={z.Estado}")),
        });

        var problemasAtivos = ListarProblemasAtivos(status.Problemas);
        campos.Add(new CampoAnalisado
        {
            Nome = "Problemas", Offset = -1, Tamanho = 0, ValorBrutoHex = "",
            ValorInterpretado = problemasAtivos.Count == 0 ? "Nenhum" : string.Join(", ", problemasAtivos),
            Descricao = null,
        });

        return campos;
    }

    private static string DescreverBateria(BatteryStatus bateria) => bateria.Tipo switch
    {
        BatteryType.SemBateria => "Sem bateria",
        BatteryType.Carregando => "Carregando",
        BatteryType.Litio => $"Lítio {bateria.PercentualLitio}%",
        BatteryType.Chumbo => $"Chumbo ~{bateria.TensaoChumboAproximada}V",
        _ => "Reservado (não usar)",
    };

    private static string DescreverPermissoesParticao(PartitionStatus p)
    {
        var itens = new List<string>();
        if (p.PermiteDesarmar) itens.Add("desarmar");
        if (p.PermiteArmar) itens.Add("armar");
        if (p.PermiteArmarStay) itens.Add("armar stay");
        if (p.PermiteArmarAway) itens.Add("armar away");
        if (p.Pronta) itens.Add("pronta");
        return itens.Count == 0 ? "nenhuma" : string.Join(", ", itens);
    }

    private static List<string> ListarProblemasAtivos(ProblemFlags p)
    {
        var mapa = new (bool Ativo, string Nome)[]
        {
            (p.BateriaFracaControleOuSensorSemFio, "Bateria fraca controle/sensor sem fio"),
            (p.SupervisaoSensor, "Supervisão de sensor"),
            (p.SaidaAuxiliar, "Saída auxiliar"),
            (p.Tamper, "Tamper"),
            (p.Dhcp, "DHCP"),
            (p.CaboDeRede, "Cabo de rede"),
            (p.ModuloCelular, "Módulo celular"),
            (p.Sms, "SMS"),
            (p.Ethernet, "Ethernet"),
            (p.Gprs, "GPRS"),
            (p.LinhaTelefonica, "Linha telefônica"),
            (p.Curto, "Curto"),
            (p.Teclado, "Teclado"),
            (p.Sirene, "Sirene"),
            (p.Bateria, "Bateria"),
            (p.Ac, "AC"),
            (p.BateriaInvertidaOuEmCurto, "Bateria invertida/em curto"),
            (p.IpDestino2, "IP destino 2"),
            (p.IpDestino1, "IP destino 1"),
            (p.ServidorDns, "Servidor DNS"),
            (p.RedeTecladoAc, "Rede teclado AC"),
            (p.SupervisaoSirene, "Supervisão de sirene"),
            (p.SenhaRedeSemFio, "Senha rede sem fio"),
            (p.AutenticacaoRedeSemFio, "Autenticação rede sem fio"),
            (p.SsidNaoEncontrado, "SSID não encontrado"),
            (p.ConflitoIp, "Conflito IP"),
            (p.Barramento, "Barramento"),
            (p.Ddns, "DDNS"),
            (p.Notificacao, "Notificação"),
            (p.ModuloEthernet, "Módulo Ethernet"),
            (p.NivelSinalOperadora, "Nível de sinal/operadora"),
            (p.ChipCelular, "Chip de celular"),
            (p.TamperTeclado, "Tamper de teclado"),
            (p.SupervisaoPgm, "Supervisão de PGM"),
        };

        return mapa.Where(x => x.Ativo).Select(x => x.Nome).ToList();
    }
}
