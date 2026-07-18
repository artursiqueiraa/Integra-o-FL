namespace CentralHub.SDK.Jfl.Protocol;

/// <summary>
/// Byte MOD enviado no comando de conexao (0x21/0x2A), identificando o modelo do
/// equipamento. Tabela retirada da secao 3.1 do protocolo oficial.
/// </summary>
public enum JflModel : byte
{
    Active32Duo = 0xA0,
    Active20Ultra = 0xA1,
    Active8Ultra = 0xA2,
    Active20Ethernet = 0xA3,
    Active100Bus = 0xA4,
    Active20Bus = 0xA5,
    ActiveFull32 = 0xA6,
    Active20 = 0xA7,
    Active8W = 0xA9,
    M300Mais = 0x4B,
    M300Flex = 0x5D,
    Qc5001WSpeed = 0x41,
    VulcanoPlus100E = 0x85,
    Ecr10W = 0x6D,
}

public static class JflModelExtensions
{
    /// <summary>Traduz o byte MOD para um nome de exibicao, mesmo que o valor nao seja reconhecido.</summary>
    public static string ToNomeAmigavel(this byte modelo) =>
        Enum.IsDefined(typeof(JflModel), modelo)
            ? ((JflModel)modelo).ToNomeAmigavel()
            : $"Desconhecido (0x{modelo:X2})";

    public static string ToNomeAmigavel(this JflModel modelo) => modelo switch
    {
        JflModel.Active32Duo => "Active 32 Duo",
        JflModel.Active20Ultra => "Active 20 Ultra",
        JflModel.Active8Ultra => "Active 8 Ultra",
        JflModel.Active20Ethernet => "Active 20 Ethernet",
        JflModel.Active100Bus => "Active 100 Bus",
        JflModel.Active20Bus => "Active 20 Bus",
        JflModel.ActiveFull32 => "Active Full 32",
        JflModel.Active20 => "Active 20",
        JflModel.Active8W => "Active 8W",
        JflModel.M300Mais => "M-300+",
        JflModel.M300Flex => "M-300 Flex",
        JflModel.Qc5001WSpeed => "QC-5001W Speed",
        JflModel.VulcanoPlus100E => "Vulcano Plus 100E",
        JflModel.Ecr10W => "ECR-10W",
        _ => $"Desconhecido (0x{(byte)modelo:X2})",
    };
}
