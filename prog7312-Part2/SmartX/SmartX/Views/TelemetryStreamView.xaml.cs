using System;
using System.Windows;
using System.Windows.Controls;
using SmartX.Collections;
using SmartX.Models;
using SmartX.Services;

namespace SmartX.Views
{
    public partial class TelemetryStreamView : UserControl
    {
        private PowerMeterValue _m1 = new(450.00);
        private PowerMeterValue _m2 = new(320.00);
        private readonly TelemetryBufferCollection _buffer = new();

        public TelemetryStreamView()
        {
            InitializeComponent();
        }

        private void BtnAggregate_Click(object sender, RoutedEventArgs e)
        {
            Random rnd = new();
            _m1 = new PowerMeterValue(rnd.Next(200, 1500));
            _m2 = new PowerMeterValue(rnd.Next(200, 1500));

            PowerMeterValue m3 = _m1 + _m2;

            TxtMeter1.Text = _m1.ToString();
            TxtMeter2.Text = _m2.ToString();
            TxtMeter3.Text = m3.ToString();
        }

        private void BtnFlushArray_Click(object sender, RoutedEventArgs e)
        {
            double[][] jaggedRawBatch = new double[][]
            {
                new double[] { 24.5, 25.1, 24.8 },
                new double[] { 102.3, 105.0 },
                new double[] { 0.88, 0.92, 0.91, 0.89 }
            };

            _buffer.IngestAndFlushArrays(jaggedRawBatch);
            DgTelemetry.ItemsSource = null;
            DgTelemetry.ItemsSource = _buffer.OptimizedList;
        }

        private void DgTelemetry_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        //  Back button — goes back to the shell (or previous overlay) 
        private void BtnBack_Click(object sender, RoutedEventArgs e)
            => NavigationService.PopOverlayOrGoToShell();

        //  Mesh shortcut — opens as a nested overlay 
        private void BtnMesh_Click(object sender, RoutedEventArgs e)
            => NavigationService.PushOverlay(new MeshMatrixView());
    }
}