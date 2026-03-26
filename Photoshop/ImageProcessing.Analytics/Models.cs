using System;

namespace ImageProcessing.Analytics
{
    public class ImageStats
    {
        public int MinIntensity { get; set; }
        public int MaxIntensity { get; set; }
        public double MeanIntensity { get; set; }
        public double StdDev { get; set; }
        public double Entropy { get; set; }
        public double Contrast { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int PixelCount { get; set; }
        public long MemoryUsageBytes { get; set; }
        public int[] Histogram { get; set; } = new int[256];
    }

    public class OperationAnalytics
    {
        public string OperationName { get; set; } = string.Empty;
        public long ExecutionTimeMs { get; set; }
        public string Parameters { get; set; } = "None";
        public string Formula { get; set; } = string.Empty;
        public ImageStats BeforeStats { get; set; } = new ImageStats();
        public ImageStats AfterStats { get; set; } = new ImageStats();

        public double[]? CurvePoints { get; set; }
        public int[]? OriginalHistogram { get; set; }
        public int[]? EqualizedHistogram { get; set; }
        public double[]? CDF { get; set; }
        public double[,]? KernelMatrix { get; set; }
        public double[,]? KernelMatrixX { get; set; }
        public double[,]? KernelMatrixY { get; set; }
        public double? EdgePixelPercentage { get; set; }
        public int? KeypointsDetected { get; set; }
        public double? HarrisThreshold { get; set; }
    }
}