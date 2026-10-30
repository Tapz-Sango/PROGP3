namespace SmartX.Api.DataStructures;

// SortedDictionary<TKey,TValue> — chronological history. Keyed on (timestamp, unique-id) so we never collide on duplicate timestamps.
public class SensorTimeline
{
    private readonly SortedDictionary<(DateTime, Guid), TelemetryPacketRecord> _timeline = new();
    private readonly object _lock = new();

    public int Count { get { lock (_lock) return _timeline.Count; } }

    public void Add(TelemetryPacketRecord packet)
    {
        lock (_lock) _timeline[(packet.Timestamp, Guid.NewGuid())] = packet;
    }

    public IEnumerable<TelemetryPacketRecord> GetRange(DateTime fromUtc, DateTime toUtc)
    {
        lock (_lock)
        {
            return _timeline
                .Where(kvp => kvp.Key.Item1 >= fromUtc && kvp.Key.Item1 <= toUtc)
                .Select(kvp => kvp.Value)
                .ToArray();
        }
    }

    public IEnumerable<TelemetryPacketRecord> Latest(int n)
    {
        lock (_lock)
        {
            return _timeline.Reverse().Take(n).Select(kvp => kvp.Value).ToArray();
        }
    }

    public IEnumerable<TelemetryPacketRecord> ForDevice(string mac, int n = 50)
    {
        lock (_lock)
        {
            return _timeline.Reverse()
                .Where(kvp => kvp.Value.MacAddress.Equals(mac, StringComparison.OrdinalIgnoreCase))
                .Take(n)
                .Select(kvp => kvp.Value)
                .ToArray();
        }
    }
}