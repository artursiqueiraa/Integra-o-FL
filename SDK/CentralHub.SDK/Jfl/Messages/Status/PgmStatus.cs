namespace CentralHub.SDK.Jfl.Messages.Status;

/// <summary>Estado de uma saida PGM (1 a 16, combinando os bytes PGM/PGM2 e P-PGM/P-PGM2).</summary>
public sealed class PgmStatus
{
    public required int Numero { get; init; }

    public required bool Acionada { get; init; }

    public required bool Permitida { get; init; }

    internal static List<PgmStatus> ParseFaixa(byte estadoBruto, byte permissoesBrutas, int numeroInicial)
    {
        var lista = new List<PgmStatus>(8);
        for (var bit = 0; bit < 8; bit++)
        {
            lista.Add(new PgmStatus
            {
                Numero = numeroInicial + bit,
                Acionada = (estadoBruto & (1 << bit)) != 0,
                Permitida = (permissoesBrutas & (1 << bit)) != 0,
            });
        }

        return lista;
    }
}
