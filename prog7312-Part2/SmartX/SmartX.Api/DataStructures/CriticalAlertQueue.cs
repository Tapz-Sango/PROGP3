namespace SmartX.Api.DataStructures;

// PriorityQueue<TPriority,TElement> — critical alerts bypass FIFO (Fist in, First Out). Lower priority number = higher urgency.
public class CriticalAlertQueue
{
    private readonly PriorityQueue<CriticalAlert, int> _pq = new();
    private readonly object _lock = new();

    public int Count { get { lock (_lock) return _pq.Count; } }

    public void Enqueue(CriticalAlert alert, int urgency)
    {
        lock (_lock) _pq.Enqueue(alert, urgency);
    }

    public bool TryDequeue(out CriticalAlert? alert)
    {
        lock (_lock)
        {
            if (_pq.TryDequeue(out var a, out _)) { alert = a; return true; }
            alert = null; return false;
        }
    }

    public IEnumerable<CriticalAlert> PeekAll()
    {
        lock (_lock) return _pq.UnorderedItems.Select(i => i.Element).ToArray();
    }
}

public class CriticalAlert
{
    public string MacAddress { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning"; // Warning | Critical | Emergency
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public static class AlertUrgency
{
    public const int Emergency = 0;
    public const int Critical = 1;
    public const int Warning = 2;
    public const int Info = 3;

    public static int FromSeverity(string s) => s switch
    {
        "Emergency" => Emergency,
        "Critical" => Critical,
        "Warning" => Warning,
        _ => Info
    };
}