namespace SmartX.Api.DataStructures;

//Learns co-occurrence patterns between user actions. Every search and every command is recorded as a node in a bigram model:    
//previous_event -> next_event (count). When a new search fires, we look up its historical follow-ups and surface the most likely next action. 
//ALSO scans live device state and surface any out-of-range reading as an immediate insight.
public class PredictiveEngine
{
    private const int MaxHistory = 1000;
    private readonly LinkedList<string> _eventHistory = new();
    private readonly Dictionary<string, Dictionary<string, int>> _bigrams = new();
    private readonly object _lock = new();

    public void RecordEvent(string eventKey)
    {
        if (string.IsNullOrWhiteSpace(eventKey)) return;
        lock (_lock)
        {
            if (_eventHistory.Count > 0)
            {
                var prev = _eventHistory.Last!.Value;
                if (!_bigrams.TryGetValue(prev, out var map))
                {
                    map = new Dictionary<string, int>();
                    _bigrams[prev] = map;
                }
                map[eventKey] = map.GetValueOrDefault(eventKey) + 1;
            }
            _eventHistory.AddLast(eventKey);
            while (_eventHistory.Count > MaxHistory) _eventHistory.RemoveFirst();
        }
    }

    public IEnumerable<Prediction> GetSuggestions(string context, int topN = 5)
    {
        lock (_lock)
        {
            if (!_bigrams.TryGetValue(context, out var map)) return Array.Empty<Prediction>();

            return map
                .OrderByDescending(kvp => kvp.Value)
                .Take(topN)
                .Select(kvp => new Prediction
                {
                    Context = context,
                    SuggestedAction = FriendlyAction(kvp.Key),
                    Confidence = Math.Min(100, kvp.Value * 20),
                    Reason = $"Performed '{FriendlyAction(kvp.Key)}' {kvp.Value} time(s) after {FriendlyContext(context)}."
                })
                .ToArray();
        }
    }

    public IEnumerable<Prediction> HighlightProblemDevices(IEnumerable<DeviceRecord> devices)
    {
        var results = new List<Prediction>();
        var now = DateTime.UtcNow;

        foreach (var d in devices)
        {
            string? issue = null;
            int confidence = 0;
            var cat = d.Category ?? "";

            if (cat.Contains("Moisture", StringComparison.OrdinalIgnoreCase) && d.LastValue < 20)
            {
                issue = $"Moisture below threshold: {d.LastValue:F1}% (safe ≥ 20%)";
                confidence = 95;
            }
            else if (cat.Contains("Power", StringComparison.OrdinalIgnoreCase) && d.LastValue > 2000)
            {
                issue = $"Power spike: {d.LastValue:F0} W (safe ≤ 2000 W)";
                confidence = 95;
            }
            else if (cat.Contains("Temperature", StringComparison.OrdinalIgnoreCase) && d.LastValue > 35)
            {
                issue = $"Overheating: {d.LastValue:F1} °C (safe ≤ 35 °C)";
                confidence = 95;
            }
            else if (d.LastSeen != default && (now - d.LastSeen).TotalMinutes > 3)
            {
                issue = $"Silent for {(now - d.LastSeen).TotalMinutes:F1} min — check power/network";
                confidence = 80;
            }

            if (issue != null)
            {
                results.Add(new Prediction
                {
                    Context = "Automated Insight",
                    SuggestedAction = $"Investigate {d.Name} ({d.MacAddress})",
                    Confidence = confidence,
                    Reason = issue
                });
            }
        }
        return results.OrderByDescending(p => p.Confidence);
    }

    private static string FriendlyAction(string key)
    {
        if (key.StartsWith("search:")) return $"Search '{key[7..]}'";
        if (key.StartsWith("command:"))
        {
            var parts = key.Split(':');
            if (parts.Length >= 3) return $"{parts[2]} on {parts[1]}";
        }
        if (key.StartsWith("category:")) return $"Filter by {key[9..]}";
        if (key.StartsWith("undo:")) return $"Undo on {key[5..]}";
        return key;
    }

    private static string FriendlyContext(string key)
    {
        if (key.StartsWith("search:")) return $"searching '{key[7..]}'";
        if (key.StartsWith("command:"))
        {
            var parts = key.Split(':');
            if (parts.Length >= 3) return $"{parts[2]} on {parts[1]}";
        }
        if (key.StartsWith("category:")) return $"filtering by {key[9..]}";
        return key;
    }
}

public class Prediction
{
    public string Context { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
}