using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SmartX.Controls
{
    //Chart
    public partial class SparklineChart : UserControl
    {
        private List<(DateTime Time, double Value, bool Critical)> _points = new();

        public SparklineChart()
        {
            InitializeComponent();
            SizeChanged += (_, __) => Redraw();
        }

        public void SetPoints(List<(DateTime Time, double Value, bool Critical)> points)
        {
            _points = points ?? new();
            Redraw();
        }

        private void Redraw()
        {
            ChartCanvas.Children.Clear();
            if (_points.Count < 2 || ChartCanvas.ActualWidth < 20 || ChartCanvas.ActualHeight < 20) return;

            double w = ChartCanvas.ActualWidth;
            double h = ChartCanvas.ActualHeight;
            double min = double.MaxValue, max = double.MinValue;
            foreach (var p in _points)
            {
                if (p.Value < min) min = p.Value;
                if (p.Value > max) max = p.Value;
            }
            double range = Math.Max(1e-6, max - min);

            // Baseline
            ChartCanvas.Children.Add(new Line
            {
                X1 = 0,
                X2 = w,
                Y1 = h - 1,
                Y2 = h - 1,
                Stroke = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
                StrokeThickness = 1
            });

            // Polyline
            var poly = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)),
                StrokeThickness = 2
            };
            double stepX = w / (_points.Count - 1);
            for (int i = 0; i < _points.Count; i++)
            {
                double x = i * stepX;
                double y = h - ((_points[i].Value - min) / range) * (h - 8) - 4;
                poly.Points.Add(new Point(x, y));
            }
            ChartCanvas.Children.Add(poly);

            // Critical dots
            for (int i = 0; i < _points.Count; i++)
            {
                if (!_points[i].Critical) continue;
                double x = i * stepX;
                double y = h - ((_points[i].Value - min) / range) * (h - 8) - 4;
                var dot = new Ellipse { Width = 6, Height = 6, Fill = Brushes.Red };
                Canvas.SetLeft(dot, x - 3);
                Canvas.SetTop(dot, y - 3);
                ChartCanvas.Children.Add(dot);
            }
        }
    }
}