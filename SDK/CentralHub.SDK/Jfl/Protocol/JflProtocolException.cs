namespace CentralHub.SDK.Jfl.Protocol;

/// <summary>Erro de parsing/formacao de um pacote ou mensagem do protocolo JFL.</summary>
public sealed class JflProtocolException : Exception
{
    public JflProtocolException(string message) : base(message)
    {
    }

    public JflProtocolException(string message, Exception inner) : base(message, inner)
    {
    }
}
