namespace CentralHub.StressTest;

/// <summary>Parâmetros de linha de comando do Stress Test — todos opcionais, com os volumes pedidos no plano de homologação como padrão.</summary>
public sealed class OpcoesStress
{
    public int Conexoes { get; init; } = 100;

    public int KeepAlives { get; init; } = 1000;

    public int Eventos { get; init; } = 500;

    public int Consultas { get; init; } = 500;

    public int Pgms { get; init; } = 100;

    public string? CaminhoSaida { get; init; }

    public static OpcoesStress Parse(string[] args)
    {
        int Ler(string nome, int padrao)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == $"--{nome}" && int.TryParse(args[i + 1], out var valor))
                {
                    return valor;
                }
            }

            return padrao;
        }

        string? LerTexto(string nome)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == $"--{nome}")
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        return new OpcoesStress
        {
            Conexoes = Ler("conexoes", 100),
            KeepAlives = Ler("keepalives", 1000),
            Eventos = Ler("eventos", 500),
            Consultas = Ler("consultas", 500),
            Pgms = Ler("pgms", 100),
            CaminhoSaida = LerTexto("saida"),
        };
    }
}
