namespace SmartX.Api.DataStructures;

// Stack<T> — LIFO history of manual override commands. Undo must never crash, so it checks Count before Pop.
public class CommandUndoStack
{
    private readonly Stack<CommandRecord> _stack = new();
    private readonly object _lock = new();

    public int Count { get { lock (_lock) return _stack.Count; } }

    public void Push(CommandRecord cmd) { lock (_lock) _stack.Push(cmd); }

    public bool TryUndo(out CommandRecord? undone)
    {
        lock (_lock)
        {
            if (_stack.Count == 0) { undone = null; return false; }
            undone = _stack.Pop();
            return true;
        }
    }

    public IEnumerable<CommandRecord> History() { lock (_lock) return _stack.ToArray(); }
}

public class CommandRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MacAddress { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty; // e.g. "PUMP_ON", "RELAY_OFF"
    public string PreviousState { get; set; } = "UNKNOWN";
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public string IssuedBy { get; set; } = "operator";
}