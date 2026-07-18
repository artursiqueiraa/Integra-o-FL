namespace CentralHub.Api.DTOs;

/// <summary>Status completo de uma Central, consultado ao vivo via comando 0x4D na sessao TCP ativa.</summary>
public class CentralStatusDto
{
    public int CentralId { get; set; }

    /// <summary>Data e hora informadas pela propria central, ou null se ela nao enviou uma data valida.</summary>
    public DateTime? DataHoraCentral { get; set; }

    public BateriaDto Bateria { get; set; } = new();

    public EletrificadorDto Eletrificador { get; set; } = new();

    public ProblemasDto Problemas { get; set; } = new();

    /// <summary>16 posicoes, numeradas 1 a 16.</summary>
    public List<ParticaoStatusDto> Particoes { get; set; } = [];

    /// <summary>99 posicoes, numeradas 1 a 99.</summary>
    public List<ZonaStatusDto> Zonas { get; set; } = [];

    /// <summary>16 posicoes, numeradas 1 a 16.</summary>
    public List<PgmStatusDto> Pgms { get; set; } = [];
}

public class BateriaDto
{
    public byte ValorBruto { get; set; }

    /// <summary>"SemBateria", "Litio", "Chumbo", "Carregando" ou "Reservado".</summary>
    public string Tipo { get; set; } = string.Empty;

    public int? PercentualLitio { get; set; }

    /// <summary>Aproximada — a JFL documenta apenas os extremos da faixa (7,2V-15,0V), nao uma formula exata.</summary>
    public double? TensaoChumboAproximada { get; set; }
}

public class EletrificadorDto
{
    /// <summary>"Desarmado", "Armado", "DesarmadoEmDisparo", "ArmadoEmDisparo", ou null se nao programado/nao documentado.</summary>
    public string? Estado { get; set; }

    public bool PermiteDesarmar { get; set; }

    public bool PermiteArmarAway { get; set; }
}

public class ParticaoStatusDto
{
    public int Numero { get; set; }

    /// <summary>Null quando a particao esta desabilitada/nao programada.</summary>
    public string? Estado { get; set; }

    public bool Desabilitada { get; set; }

    public bool PermiteDesarmar { get; set; }

    public bool PermiteArmar { get; set; }

    public bool PermiteArmarStay { get; set; }

    public bool PermiteArmarAway { get; set; }

    public bool Pronta { get; set; }
}

public class ZonaStatusDto
{
    public int Numero { get; set; }

    /// <summary>Null quando o valor recebido nao corresponde a nenhum estado documentado.</summary>
    public string? Estado { get; set; }

    public bool PermiteInibir { get; set; }
}

public class PgmStatusDto
{
    public int Numero { get; set; }

    public bool Acionada { get; set; }

    public bool Permitida { get; set; }
}

public class ProblemasDto
{
    public bool BateriaFracaControleOuSensorSemFio { get; set; }
    public bool SupervisaoSensor { get; set; }
    public bool SaidaAuxiliar { get; set; }
    public bool Tamper { get; set; }
    public bool Dhcp { get; set; }
    public bool CaboDeRede { get; set; }
    public bool ModuloCelular { get; set; }
    public bool Sms { get; set; }

    public bool Ethernet { get; set; }
    public bool Gprs { get; set; }
    public bool LinhaTelefonica { get; set; }
    public bool Curto { get; set; }
    public bool Teclado { get; set; }
    public bool Sirene { get; set; }
    public bool Bateria { get; set; }

    /// <summary>Problema de alimentacao AC (energia da rede eletrica).</summary>
    public bool Ac { get; set; }

    public bool BateriaInvertidaOuEmCurto { get; set; }
    public bool IpDestino2 { get; set; }
    public bool IpDestino1 { get; set; }
    public bool ServidorDns { get; set; }
    public bool RedeTecladoAc { get; set; }
    public bool SupervisaoSirene { get; set; }
    public bool SenhaRedeSemFio { get; set; }
    public bool AutenticacaoRedeSemFio { get; set; }

    public bool SsidNaoEncontrado { get; set; }
    public bool ConflitoIp { get; set; }
    public bool Barramento { get; set; }
    public bool Ddns { get; set; }
    public bool Notificacao { get; set; }
    public bool ModuloEthernet { get; set; }
    public bool NivelSinalOperadora { get; set; }
    public bool ChipCelular { get; set; }

    public bool TamperTeclado { get; set; }
    public bool SupervisaoPgm { get; set; }

    /// <summary>Conveniencia: inverso de <see cref="Ac"/>.</summary>
    public bool AlimentacaoAcNormal { get; set; }
}
