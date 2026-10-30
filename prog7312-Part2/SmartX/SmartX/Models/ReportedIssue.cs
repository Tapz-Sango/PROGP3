using System;

namespace SmartX.Models
{
    public class ReportedIssue
    {
        // What the issue is about which defaults to empty instead of null
        public string Description { get; set; } = string.Empty;

        // When the issue was reported this defaults to the current UTC time
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Shows the issue as "[HH:mm:ss] description" when printed
        public override string ToString() => $"[{Timestamp:HH:mm:ss}] {Description}";
    }
}