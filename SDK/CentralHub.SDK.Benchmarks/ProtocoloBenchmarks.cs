using BenchmarkDotNet.Attributes;
using CentralHub.SDK.Jfl.Messages.Status;
using CentralHub.SDK.Jfl.Protocol;

namespace CentralHub.SDK.Benchmarks;

/// <summary>
/// Benchmarks da camada de framing/protocolo (Fase 0.7 do plano de homologação) — mede o
/// custo de operações já homologadas (nunca alteradas aqui), usadas como referência para
/// detectar regressões de performance ao longo das próximas fases.
/// </summary>
[MemoryDiagnoser]
public class ProtocoloBenchmarks
{
    // Captura real do manual (secao 3.5): comando de keep-alive.
    private static readonly byte[] PacoteKeepAlive = Convert.FromHexString("7B05184026");

    // Payload sintetico de 115 bytes (formato 4.10) -- so o tempo de parse importa aqui, a
    // corretude byte a byte ja e coberta por CentralStatusResponseTests.cs.
    private readonly byte[] _payloadStatus = new byte[115];

    private byte[] _pacoteStatusCompleto = [];

    [GlobalSetup]
    public void Setup()
    {
        _pacoteStatusCompleto = PacketBuilder.Build(seq: 0x01, cmd: 0x4D, _payloadStatus);
    }

    [Benchmark]
    public byte Checksum_Calculate() => ChecksumCalculator.Calculate(PacoteKeepAlive.AsSpan(0, 4));

    [Benchmark]
    public bool Checksum_IsValid() => ChecksumCalculator.IsValid(PacoteKeepAlive);

    [Benchmark]
    public byte[] PacketBuilder_Build_KeepAlive() => PacketBuilder.Build(seq: 0x18, cmd: 0x40, ReadOnlySpan<byte>.Empty);

    [Benchmark]
    public byte[] PacketBuilder_Build_RespostaStatus() => PacketBuilder.Build(seq: 0x01, cmd: 0x4D, _payloadStatus);

    [Benchmark]
    public JflParseResult PacketParser_TryParse_KeepAlive() => PacketParser.TryParse(PacoteKeepAlive);

    [Benchmark]
    public JflParseResult PacketParser_TryParse_RespostaStatus() => PacketParser.TryParse(_pacoteStatusCompleto);

    [Benchmark]
    public CentralStatusResponse CentralStatusResponse_Parse() => CentralStatusResponse.Parse(_payloadStatus);
}
