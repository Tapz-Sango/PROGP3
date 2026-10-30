using System;

namespace SmartX.Models
{
    public struct TelemetryPacket<T> where T : struct
    {
        public string SensorId { get; set; }
        public T Value { get; set; }
        public string Unit { get; set; }
        public DateTime Timestamp { get; set; }

        public TelemetryPacket(string sensorId, T value, string unit)
        {
            SensorId = sensorId;
            Value = value;
            Unit = unit;
            Timestamp = DateTime.UtcNow;
        }
    }

    // Full operator set which fixes Part 1 feedback [added remaining comparison operators and equality
    public struct PowerMeterValue : IEquatable<PowerMeterValue>, IComparable<PowerMeterValue>
    {
        public double Wattage { get; set; }

        public PowerMeterValue(double wattage)
        {
            Wattage = wattage;
        }

        // Arithmetic
        public static PowerMeterValue operator +(PowerMeterValue a, PowerMeterValue b)
            => new(a.Wattage + b.Wattage);

        public static PowerMeterValue operator -(PowerMeterValue a, PowerMeterValue b)
            => new(Math.Max(0, a.Wattage - b.Wattage));

        public static PowerMeterValue operator *(PowerMeterValue a, double factor)
            => new(a.Wattage * factor);

        public static PowerMeterValue operator /(PowerMeterValue a, double divisor)
            => divisor == 0 ? new(0) : new(a.Wattage / divisor);

        // Comparisons (against PowerMeterValue)
        public static bool operator >(PowerMeterValue a, PowerMeterValue b) => a.Wattage > b.Wattage;
        public static bool operator <(PowerMeterValue a, PowerMeterValue b) => a.Wattage < b.Wattage;
        public static bool operator >=(PowerMeterValue a, PowerMeterValue b) => a.Wattage >= b.Wattage;
        public static bool operator <=(PowerMeterValue a, PowerMeterValue b) => a.Wattage <= b.Wattage;
        public static bool operator ==(PowerMeterValue a, PowerMeterValue b) => Math.Abs(a.Wattage - b.Wattage) < 0.0001;
        public static bool operator !=(PowerMeterValue a, PowerMeterValue b) => !(a == b);

        // Comparisons (against raw double — keeps Part 1 behaviour)
        public static bool operator >(PowerMeterValue a, double t) => a.Wattage > t;
        public static bool operator <(PowerMeterValue a, double t) => a.Wattage < t;
        public static bool operator >=(PowerMeterValue a, double t) => a.Wattage >= t;
        public static bool operator <=(PowerMeterValue a, double t) => a.Wattage <= t;
        public static bool operator ==(PowerMeterValue a, double t) => Math.Abs(a.Wattage - t) < 0.0001;
        public static bool operator !=(PowerMeterValue a, double t) => !(a == t);

        // Equality contract
        public bool Equals(PowerMeterValue other) => this == other;
        public override bool Equals(object? obj) => obj is PowerMeterValue other && Equals(other);
        public override int GetHashCode() => Wattage.GetHashCode();

        // Ordering contract
        public int CompareTo(PowerMeterValue other) => Wattage.CompareTo(other.Wattage);

        public override string ToString() => $"{Wattage:F2} W";
    }
}