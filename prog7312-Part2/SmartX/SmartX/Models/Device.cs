using System;

namespace SmartX.Models
{
    //Device Reg/Recog
    public class Device
    {
        public string MacAddress { get; set; } = "";
        public string Name { get; set; } = "";
        public string Location { get; set; } = "";
        public string Category { get; set; } = "";
        public string AlertState { get; set; } = "Normal";
        public DateTime RegisteredAt { get; set; }
        public DateTime LastSeen { get; set; }
        public double LastValue { get; set; }
        public string LastUnit { get; set; } = "";
    }
}