using System.Collections.Generic;
using System.Collections.ObjectModel;
using SmartX.Models;

namespace SmartX.Collections
{
    public class TelemetryBufferCollection
    {
        // Main storage for telemetry packets
        private readonly List<TelemetryPacket<double>> _optimizedList = new List<TelemetryPacket<double>>();

        // Holds the last batch of raw data received
        public double[][]? JaggedRawStream { get; private set; }

        // Exposes the list as read-only and cannot be modified externally
        public ReadOnlyCollection<TelemetryPacket<double>> OptimizedList => _optimizedList.AsReadOnly();

        // Takes a fresh batch of raw jagged data and converts it into telemetry packets
        public void IngestAndFlushArrays(double[][] rawJaggedData)
        {
            JaggedRawStream = rawJaggedData;
            _optimizedList.Clear();

            // Walk through each node (row) and each value in that row, wrapping every value into a TelemetryPacket with a generated node name
            for (int i = 0; i < rawJaggedData.Length; i++)
            {
                for (int j = 0; j < rawJaggedData[i].Length; j++)
                {
                    double val = rawJaggedData[i][j];
                    _optimizedList.Add(new TelemetryPacket<double>($"NODE-00{i + 1}", val, "m³/s"));
                }
            }
        }
    }
}