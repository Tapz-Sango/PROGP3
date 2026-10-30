namespace SmartX.Api.DataStructures;

// HashSet<T> — unique active error states. This also guarantees no duplicate alert fires for the same node+state pair.
public class DisconnectedNodes
{
    private readonly HashSet<string> _activeErrors = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public int Count { get { lock (_lock) return _activeErrors.Count; } }

    // Returns TRUE only on first insert -> caller fires alert only once.
    public bool MarkActive(string mac, string errorKey)
    {
        lock (_lock) return _activeErrors.Add($"{mac}::{errorKey}");
    }

    public bool Clear(string mac, string errorKey)
    {
        lock (_lock) return _activeErrors.Remove($"{mac}::{errorKey}");
    }

    public bool IsActive(string mac, string errorKey)
    {
        lock (_lock) return _activeErrors.Contains($"{mac}::{errorKey}");
    }

    public IEnumerable<string> All()
    {
        lock (_lock) return _activeErrors.ToArray();
    }
}