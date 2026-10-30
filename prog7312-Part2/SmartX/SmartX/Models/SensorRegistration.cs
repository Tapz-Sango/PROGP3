using System;

namespace SmartX.Models
{
    //Registering the Sensors
    public class SensorRegistration
    {
        // Unique address used to identify the sensor
        public string MacAddress { get; set; } = string.Empty;

        // Physical or logical place where the sensor is installed
        public string Location { get; set; } = string.Empty;

        // Type or group the sensor belongs to
        public string Category { get; set; } = string.Empty;

        // When the sensor was registered.
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    }
}