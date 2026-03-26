using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace ImageProcessing.Analytics
{
    public static class ImageAnalyzer
    {
        public static ImageStats CalculateStats(Bitmap bmp)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            int totalPixels = width * height;
            int[] hist = new int[256];

            double sum = 0;
            double sumSq = 0;
            int min = 255;
            int max = 0;

            BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            unsafe
            {
                byte* ptr = (byte*)data.Scan0;
                for (int y = 0; y < height; y++)
                {
                    byte* row = ptr + y * data.Stride;
                    for (int x = 0; x < width * 4; x += 4)
                    {
                        int gray = (int)(0.299 * row[x + 2] + 0.587 * row[x + 1] + 0.114 * row[x]);
                        hist[gray]++;
                        sum += gray;
                        sumSq += gray * gray;
                        if (gray < min) min = gray;
                        if (gray > max) max = gray;
                    }
                }
            }
            bmp.UnlockBits(data);

            double mean = sum / totalPixels;
            double variance = (sumSq / totalPixels) - (mean * mean);
            double stdDev = Math.Sqrt(Math.Max(0, variance));

            double entropy = 0;
            for (int i = 0; i < 256; i++)
            {
                double p = (double)hist[i] / totalPixels;
                if (p > 0) entropy -= p * Math.Log2(p);
            }

            return new ImageStats
            {
                MinIntensity = min,
                MaxIntensity = max,
                MeanIntensity = mean,
                StdDev = stdDev,
                Entropy = entropy,
                Contrast = stdDev,
                Width = width,
                Height = height,
                PixelCount = totalPixels,
                MemoryUsageBytes = totalPixels * 4L,
                Histogram = hist
            };
        }

        public static double CalculateEdgePercentage(Bitmap bmp, int threshold = 50)
        {
            int edgeCount = 0;
            int width = bmp.Width;
            int height = bmp.Height;
            int total = width * height;
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            unsafe
            {
                byte* ptr = (byte*)data.Scan0;
                for (int y = 0; y < height; y++)
                {
                    byte* row = ptr + y * data.Stride;
                    for (int x = 0; x < width * 4; x += 4)
                    {
                        if (row[x] > threshold) edgeCount++;
                    }
                }
            }
            bmp.UnlockBits(data);
            return (edgeCount / (double)total) * 100.0;
        }

        public static int CountHarrisKeypoints(Bitmap resultBmp)
        {
            int count = 0;
            int width = resultBmp.Width;
            int height = resultBmp.Height;
            BitmapData data = resultBmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            unsafe
            {
                byte* ptr = (byte*)data.Scan0;
                for (int y = 0; y < height; y++)
                {
                    byte* row = ptr + y * data.Stride;
                    for (int x = 0; x < width * 4; x += 4)
                    {
                        if (row[x] == 0 && row[x + 1] == 0 && row[x + 2] == 255) count++;
                    }
                }
            }
            resultBmp.UnlockBits(data);
            return count / 9;
        }
    }
}