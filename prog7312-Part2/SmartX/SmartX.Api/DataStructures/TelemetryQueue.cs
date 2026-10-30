using System.Collections.Concurrent;

namespace SmartX.Api.DataStructures;

// Queue<T> — FIFO(First in, First out) processing of incoming telemetry.
public class TelemetryQueue
{
    private readonly ConcurrentQueue<TelemetryPacketRecord> _queue = new();

    public int Count => _queue.Count;

    public void Enqueue(TelemetryPacketRecord packet) => _queue.Enqueue(packet);

    public bool TryDequeue(out TelemetryPacketRecord? packet) => _queue.TryDequeue(out packet);

    public IEnumerable<TelemetryPacketRecord> PeekAll() => _queue.ToArray();
}

public class TelemetryPacketRecord
{
    public string MacAddress { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsCritical { get; set; }
}