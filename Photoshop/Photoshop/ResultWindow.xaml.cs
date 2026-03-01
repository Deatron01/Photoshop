using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ImageProcessing.Analytics;

namespace ImageProcessing.WpfApp
{
    public partial class ResultWindow : Window
    {
        public ResultWindow(BitmapImage imageSource, OperationAnalytics analytics)
        {
            InitializeComponent();
            ResultImage.Source = imageSource;
            PopulateDashboard(analytics);
        }

        private void PopulateDashboard(OperationAnalytics a)
        {
            TxtTitle.Text = a.OperationName;
            TxtExecutionTime.Text = $"Execution Time: {a.ExecutionTimeMs} ms";
            TxtParameters.Text = $"Parameters: {a.Parameters}";
            TxtFormula.Text = $"Formula: {a.Formula}";

            TxtDimensions.Text = $"Dimensions: {a.AfterStats.Width} x {a.AfterStats.Height}";
            TxtPixelCount.Text = $"Pixel Count: {a.AfterStats.PixelCount:N0}";
            TxtMemoryUsage.Text = $"Estimated Memory: {a.AfterStats.MemoryUsageBytes / 1024.0 / 1024.0:F2} MB";

            TxtMinB.Text = a.BeforeStats.MinIntensity.ToString(); TxtMinA.Text = a.AfterStats.MinIntensity.ToString();
            TxtMaxB.Text = a.BeforeStats.MaxIntensity.ToString(); TxtMaxA.Text = a.AfterStats.MaxIntensity.ToString();
            TxtMeanB.Text = a.BeforeStats.MeanIntensity.ToString("F2"); TxtMeanA.Text = a.AfterStats.MeanIntensity.ToString("F2");
            TxtStdDevB.Text = a.BeforeStats.StdDev.ToString("F2"); TxtStdDevA.Text = a.AfterStats.StdDev.ToString("F2");
            TxtEntropyB.Text = a.BeforeStats.Entropy.ToString("F2"); TxtEntropyA.Text = a.AfterStats.Entropy.ToString("F2");
            TxtContrastB.Text = a.BeforeStats.Contrast.ToString("F2"); TxtContrastA.Text = a.AfterStats.Contrast.ToString("F2");

            HandleSpecificInsights(a);
        }

        private void HandleSpecificInsights(OperationAnalytics a)
        {
            SpecificInsightsPanel.Children.Clear();

            if (a.CurvePoints != null)
            {
                TxtChartLegend.Text = "Input -> Output Curve";
                DrawCurve(a.CurvePoints, Brushes.Yellow);
            }
            else if (a.EqualizedHistogram != null && a.CDF != null)
            {
                TxtChartLegend.Text = "Gray: Original | Cyan: Equalized | Pink: CDF";
                DrawHistogram(a.OriginalHistogram!, Brushes.Gray, 0.5);
                DrawHistogram(a.EqualizedHistogram, Brushes.Cyan, 0.8);
                DrawCurve(a.CDF.Select(v => v * 255).ToArray(), Brushes.DeepPink);
            }
            else
            {
                TxtChartLegend.Text = "Output Histogram";
                DrawHistogram(a.AfterStats.Histogram, Brushes.DodgerBlue, 1.0);
            }

            if (a.KernelMatrix != null) AddMatrixView("Kernel Matrix", a.KernelMatrix);
            if (a.KernelMatrixX != null) AddMatrixView("Kernel Gx", a.KernelMatrixX);
            if (a.KernelMatrixY != null) AddMatrixView("Kernel Gy", a.KernelMatrixY);

            if (a.EdgePixelPercentage.HasValue)
                AddTextInsight($"Edge Pixels: {a.EdgePixelPercentage.Value:F2}%");

            if (a.KeypointsDetected.HasValue)
                AddTextInsight($"Keypoints Detected: {a.KeypointsDetected.Value}");

            if (SpecificInsightsPanel.Children.Count > 0)
                GrpSpecificInsights.Visibility = Visibility.Visible;
        }

        private void AddTextInsight(string text)
        {
            SpecificInsightsPanel.Children.Add(new TextBlock { Text = text, Foreground = Brushes.LightGreen, FontWeight = FontWeights.Bold });
        }

        private void AddMatrixView(string title, double[,] matrix)
        {
            SpecificInsightsPanel.Children.Add(new TextBlock { Text = title, Margin = new Thickness(0, 5, 0, 2), Foreground = Brushes.Silver });
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);

            var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            for (int i = 0; i < cols; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (int i = 0; i < rows; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    var tb = new TextBlock { Text = matrix[r, c].ToString("0.###"), Margin = new Thickness(5), Foreground = Brushes.White };
                    Grid.SetRow(tb, r);
                    Grid.SetColumn(tb, c);
                    grid.Children.Add(tb);
                }
            }
            SpecificInsightsPanel.Children.Add(grid);
        }

        private void DrawHistogram(int[] hist, Brush color, double opacity)
        {
            // Explicitly set the canvas bounds just in case the layout engine hasn't measured it yet
            ChartCanvas.Width = 350;
            ChartCanvas.Height = 180;

            double canvasWidth = 350;
            double canvasHeight = 180;

            // Smart Scaling: Sort to find the peaks. 
            // If the biggest peak is massively larger than the 2nd biggest (a solid background),
            // we scale using the 2nd biggest peak so the rest of the chart doesn't get squashed flat.
            var sortedHist = hist.OrderByDescending(v => v).ToList();
            double max = sortedHist[0];
            double scaleMax = max;
            if (sortedHist[1] > 0 && max > sortedHist[1] * 10)
            {
                scaleMax = sortedHist[1] * 1.2; // Use 2nd peak + 20% headroom
            }
            if (scaleMax == 0) scaleMax = 1;

            // Use a PointCollection for a filled Polygon (much better performance and visibility than Lines)
            var points = new PointCollection();
            points.Add(new Point(0, canvasHeight)); // Start at bottom-left

            for (int i = 0; i < 256; i++)
            {
                double h = (hist[i] / scaleMax) * canvasHeight;
                if (h > canvasHeight) h = canvasHeight; // Cap the giant background spikes at the ceiling

                double x = (i / 255.0) * canvasWidth;
                double y = canvasHeight - h;

                points.Add(new Point(x, y));
            }

            points.Add(new Point(canvasWidth, canvasHeight)); // End at bottom-right

            var polygon = new Polygon
            {
                Points = points,
                Fill = color,
                Opacity = opacity,
                Stroke = color, // Adding stroke makes thin peaks pop out more
                StrokeThickness = 1
            };

            ChartCanvas.Children.Add(polygon);
        }

        private void DrawCurve(double[] pointsArray, Brush color)
        {
            ChartCanvas.Width = 350;
            ChartCanvas.Height = 180;

            double canvasWidth = 350;
            double canvasHeight = 180;
            var points = new PointCollection();

            for (int i = 0; i < pointsArray.Length; i++)
            {
                double x = (i / (double)(pointsArray.Length - 1)) * canvasWidth;
                double y = canvasHeight - ((pointsArray[i] / 255.0) * canvasHeight);
                points.Add(new Point(x, y));
            }

            // Polyline ensures a continuous, unbroken curve
            var polyline = new Polyline
            {
                Points = points,
                Stroke = color,
                StrokeThickness = 2.5
            };

            ChartCanvas.Children.Add(polyline);
        }
    }
}