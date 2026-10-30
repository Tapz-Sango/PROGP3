using System.Collections.Concurrent;

namespace SmartX.Api.DataStructures;

// Dictionary<TKey,TValue> — O(1) looks up the device by MAC 
public class DeviceRegistry
{
    private readonly ConcurrentDictionary<string, DeviceRecord> _devices = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _devices.Count;
    public IEnumerable<DeviceRecord> All => _devices.Values;

    public bool TryGet(string mac, out DeviceRecord? device) => _devices.TryGetValue(Normalise(mac), out device);

    public DeviceRecord Upsert(DeviceRecord device)
    {
        device.MacAddress = Normalise(device.MacAddress);
        _devices.AddOrUpdate(device.MacAddress, device, (_, existing) =>
        {
            if (!string.IsNullOrWhiteSpace(device.Name)) existing.Name = device.Name;
            if (!string.IsNullOrWhiteSpace(device.Location)) existing.Location = device.Location;
            if (!string.IsNullOrWhiteSpace(device.Category)) existing.Category = device.Category;
            if (!string.IsNullOrWhiteSpace(device.AlertState)) existing.AlertState = device.AlertState;
            existing.LastSeen = DateTime.UtcNow;
            return existing;
        });
        return _devices[device.MacAddress];
    }

    // O(1) update for a telemetry packet(Key for dictionary).
    public void UpdateReading(string mac, double value, string unit, string alertState)
    {
        var key = Normalise(mac);
        if (_devices.TryGetValue(key, out var d))
        {
            d.LastValue = value;
            d.LastUnit = unit;
            d.LastSeen = DateTime.UtcNow;
            d.AlertState = alertState;
        }
    }

    public bool Remove(string mac) => _devices.TryRemove(Normalise(mac), out _);

    public IEnumerable<DeviceRecord> FilterByCategory(string? category) =>
        string.IsNullOrWhiteSpace(category) || category.Equals("All", StringComparison.OrdinalIgnoreCase)
            ? _devices.Values
            : _devices.Values.Where(d => d.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<DeviceRecord> FilterByAlertState(string? state) =>
        string.IsNullOrWhiteSpace(state) || state.Equals("All", StringComparison.OrdinalIgnoreCase)
            ? _devices.Values
            : _devices.Values.Where(d => d.AlertState.Equals(state, StringComparison.OrdinalIgnoreCase));

    private static string Normalise(string mac) => (mac ?? "").Replace('-', ':').Trim().ToUpperInvariant();
}

public class DeviceRecord
{
    public string MacAddress { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string AlertState { get; set; } = "Normal";
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public double LastValue { get; set; }
    public string LastUnit { get; set; } = string.Empty;
}