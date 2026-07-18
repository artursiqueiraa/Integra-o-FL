namespace CentralHub.SDK.Jfl.Server;

/// <summary>
/// Decide se um numero de serie pode se conectar (campo RESULT do comando de
/// conexao). Ponto de extensao para uma implementacao futura que consulte o
/// cadastro de Centrais; por enquanto o SDK fornece <see cref="LiberarTodasCentraisAuthorizationProvider"/>
/// como padrao, que libera qualquer numero de serie.
/// </summary>
public interface ICentralAuthorizationProvider
{
    Task<bool> EstaLiberadaAsync(string numeroSerie, CancellationToken cancellationToken);
}

/// <summary>Implementacao padrao: libera qualquer central que se conectar. Sem regra de negocio.</summary>
public sealed class LiberarTodasCentraisAuthorizationProvider : ICentralAuthorizationProvider
{
    public Task<bool> EstaLiberadaAsync(string numeroSerie, CancellationToken cancellationToken) => Task.FromResult(true);
}
