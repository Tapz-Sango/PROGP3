using System;

namespace SmartX.Models
{
    //Commands
    public class CommandRecord
    {
        public Guid Id { get; set; }
        public string MacAddress { get; set; } = "";
        public string DeviceName { get; set; } = "";
        public string Command { get; set; } = "";
        public string PreviousState { get; set; } = "";
        public DateTime IssuedAt { get; set; }
    }
}