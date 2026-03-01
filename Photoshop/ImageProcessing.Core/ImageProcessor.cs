using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ImageProcessing.Core
{
    public static class ImageProcessor
    {
        // 1. Image Negation
        public static Bitmap Negate(Bitmap source)
        {
            return ProcessPointOp(source, (r, g, b) =>
                ((byte)(255 - r), (byte)(255 - g), (byte)(255 - b)));
        }

        // 2. Gamma Correction (using LUT for performance)
        public static Bitmap GammaCorrection(Bitmap source, double gamma)
        {
            byte[] lut = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                double val = Math.Pow(i / 255.0, gamma) * 255.0;
                lut[i] = (byte)Math.Clamp(val, 0, 255);
            }
            return ProcessPointOp(source, (r, g, b) => (lut[r], lut[g], lut[b]));
        }

        // 3. Logarithmic Transformation
        public static Bitmap LogTransform(Bitmap source)
        {
            byte[] lut = new byte[256];
            double c = 255.0 / Math.Log(256);
            for (int i = 0; i < 256; i++)
            {
                lut[i] = (byte)Math.Clamp(c * Math.Log(1 + i), 0, 255);
            }
            return ProcessPointOp(source, (r, g, b) => (lut[r], lut[g], lut[b]));
        }

        // 4. Grayscale Conversion
        public static Bitmap ToGrayscale(Bitmap source)
        {
            return ProcessPointOp(source, (r, g, b) =>
            {
                byte gray = (byte)(0.299 * r + 0.587 * g + 0.114 * b);
                return (gray, gray, gray);
            });
        }

        // 5. Histogram Creation (Returns rendered Histogram Image)
        public static Bitmap GetHistogram(Bitmap source)
        {
            int[] hist = new int[256];
            int width = source.Width;
            int height = source.Height;

            BitmapData data = source.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            unsafe
            {
                byte* ptr = (byte*)data.Scan0;
                int bytes = data.Stride * height;
                for (int i = 0; i < bytes; i += 4)
                {
                    // Using fast perceived luminance
                    int gray = (int)(0.299 * ptr[i + 2] + 0.587 * ptr[i + 1] + 0.114 * ptr[i]);
                    hist[gray]++;
                }
            }
            source.UnlockBits(data);

            return DrawHistogram(hist);
        }

        // 6. Histogram Equalization
        public static Bitmap HistogramEqualization(Bitmap source)
        {
            int[] hist = new int[256];
            int width = source.Width;
            int height = source.Height;
            int totalPixels = width * height;

            BitmapData srcData = source.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            unsafe
            {
                byte* ptr = (byte*)srcData.Scan0;
                int bytes = srcData.Stride * height;
                for (int i = 0; i < bytes; i += 4)
                {
                    int gray = (int)(0.299 * ptr[i + 2] + 0.587 * ptr[i + 1] + 0.114 * ptr[i]);
                    hist[gray]++;
                }
            }

            // CDF (Cumulative Distribution Function)
            int[] cdf = new int[256];
            cdf[0] = hist[0];
            for (int i = 1; i < 256; i++) cdf[i] = cdf[i - 1] + hist[i];

            byte[] lut = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                lut[i] = (byte)Math.Clamp(Math.Round((double)cdf[i] / totalPixels * 255.0), 0, 255);
            }

            Bitmap result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData dstData = result.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            unsafe
            {
                byte* srcPtr = (byte*)srcData.Scan0;
                byte* dstPtr = (byte*)dstData.Scan0;
                Parallel.For(0, height, y =>
                {
                    Span<byte> srcRow = new Span<byte>(srcPtr + y * srcData.Stride, srcData.Stride);
                    Span<byte> dstRow = new Span<byte>(dstPtr + y * dstData.Stride, dstData.Stride);

                    for (int x = 0; x < width * 4; x += 4)
                    {
                        byte gray = (byte)(0.299 * srcRow[x + 2] + 0.587 * srcRow[x + 1] + 0.114 * srcRow[x]);
                        byte eq = lut[gray];
                        dstRow[x] = eq; dstRow[x + 1] = eq; dstRow[x + 2] = eq; dstRow[x + 3] = 255;
                    }
                });
            }
            source.UnlockBits(srcData);
            result.UnlockBits(dstData);
            return result;
        }

        // 7 & 8. Convolution Filters (Box / Gaussian)
        public static Bitmap BoxFilter(Bitmap source) => Convolve(source, new double[,] { { 1, 1, 1 }, { 1, 1, 1 }, { 1, 1, 1 } }, 1.0 / 9.0);

        public static Bitmap GaussianFilter(Bitmap source) => Convolve(source, new double[,] { { 1, 2, 1 }, { 2, 4, 2 }, { 1, 2, 1 } }, 1.0 / 16.0);

        // 9. Sobel Edge Detection
        public static Bitmap Sobel(Bitmap source)
        {
            double[,] gx = { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
            double[,] gy = { { 1, 2, 1 }, { 0, 0, 0 }, { -1, -2, -1 } };
            return ConvolveMagnitude(source, gx, gy);
        }

        // 10. Laplace Edge Detection
        public static Bitmap Laplace(Bitmap source) => Convolve(source, new double[,] { { 0, 1, 0 }, { 1, -4, 1 }, { 0, 1, 0 } }, 1.0);

        // 11. Harris Keypoint Detection (Corrected with Windowing & NMS)
        public static Bitmap HarrisCorners(Bitmap source)
        {
            Bitmap grayImg = ToGrayscale(source);
            int w = grayImg.Width;
            int h = grayImg.Height;

            // Intermediate buffers for derivatives
            int[] ix2 = new int[w * h];
            int[] iy2 = new int[w * h];
            int[] ixy = new int[w * h];

            BitmapData srcData = grayImg.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int stride = srcData.Stride;

            unsafe
            {
                byte* src = (byte*)srcData.Scan0;

                // Pass 1: Compute Sobel Derivatives
                Parallel.For(1, h - 1, y =>
                {
                    for (int x = 1; x < w - 1; x++)
                    {
                        int idx = y * stride + x * 4;
                        int pIdx = y * w + x;

                        int ix = (src[idx - stride + 4] + 2 * src[idx + 4] + src[idx + stride + 4]) -
                                 (src[idx - stride - 4] + 2 * src[idx - 4] + src[idx + stride - 4]);

                        int iy = (src[idx + stride - 4] + 2 * src[idx + stride] + src[idx + stride + 4]) -
                                 (src[idx - stride - 4] + 2 * src[idx - stride] + src[idx - stride + 4]);

                        ix2[pIdx] = ix * ix;
                        iy2[pIdx] = iy * iy;
                        ixy[pIdx] = ix * iy;
                    }
                });
            }
            grayImg.UnlockBits(srcData);

            // Pass 2: Apply 3x3 Window Summation & Calculate R
            double[] rMap = new double[w * h];
            double maxR = 0;
            double k = 0.04;
            object maxRLock = new object();

            Parallel.For(2, h - 2, y =>
            {
                double localMaxR = 0;
                for (int x = 2; x < w - 2; x++)
                {
                    long sumX2 = 0, sumY2 = 0, sumXY = 0;

                    // 3x3 Local Neighborhood Window
                    for (int wy = -1; wy <= 1; wy++)
                    {
                        int rowOff = (y + wy) * w;
                        for (int wx = -1; wx <= 1; wx++)
                        {
                            int pIdx = rowOff + (x + wx);
                            sumX2 += ix2[pIdx];
                            sumY2 += iy2[pIdx];
                            sumXY += ixy[pIdx];
                        }
                    }

                    double det = (sumX2 * sumY2) - (sumXY * sumXY);
                    double trace = sumX2 + sumY2;
                    double r = det - k * (trace * trace);

                    rMap[y * w + x] = r;
                    if (r > localMaxR) localMaxR = r;
                }

                lock (maxRLock) { if (localMaxR > maxR) maxR = localMaxR; }
            });

            // Pass 3: Non-Maximum Suppression (NMS) and Drawing
            Bitmap result = (Bitmap)source.Clone();
            BitmapData dstData = result.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            // Dynamic threshold: Top 1% of the strongest corner response
            double dynamicThreshold = maxR * 0.01;
            if (dynamicThreshold < 100000) dynamicThreshold = 100000; // Fallback for flat images

            unsafe
            {
                byte* dst = (byte*)dstData.Scan0;

                Parallel.For(3, h - 3, y =>
                {
                    for (int x = 3; x < w - 3; x++)
                    {
                        int pIdx = y * w + x;
                        double r = rMap[pIdx];

                        if (r > dynamicThreshold)
                        {
                            bool isLocalMax = true;

                            // 3x3 NMS window to isolate the exact pixel
                            for (int wy = -1; wy <= 1; wy++)
                            {
                                for (int wx = -1; wx <= 1; wx++)
                                {
                                    if (wx == 0 && wy == 0) continue;
                                    if (rMap[(y + wy) * w + (x + wx)] > r)
                                    {
                                        isLocalMax = false;
                                        break;
                                    }
                                }
                                if (!isLocalMax) break;
                            }

                            if (isLocalMax)
                            {
                                // Draw a 3x3 red dot to make the corner clearly visible
                                for (int dy = -1; dy <= 1; dy++)
                                {
                                    for (int dx = -1; dx <= 1; dx++)
                                    {
                                        int drawIdx = (y + dy) * stride + (x + dx) * 4;
                                        dst[drawIdx] = 0;       // B
                                        dst[drawIdx + 1] = 0;   // G
                                        dst[drawIdx + 2] = 255; // R
                                    }
                                }
                            }
                        }
                    }
                });
            }
            result.UnlockBits(dstData);
            return result;
        }

        // --- Helper Methods ---

        private static Bitmap ProcessPointOp(Bitmap source, Func<byte, byte, byte, (byte r, byte g, byte b)> operation)
        {
            int width = source.Width;
            int height = source.Height;
            Bitmap result = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            BitmapData srcData = source.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData dstData = result.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            unsafe
            {
                byte* srcPtr = (byte*)srcData.Scan0;
                byte* dstPtr = (byte*)dstData.Scan0;

                Parallel.For(0, height, y =>
                {
                    // Span wrapped for bounds-safe internal row processing
                    Span<byte> srcRow = new Span<byte>(srcPtr + y * srcData.Stride, srcData.Stride);
                    Span<byte> dstRow = new Span<byte>(dstPtr + y * dstData.Stride, dstData.Stride);

                    for (int x = 0; x < width * 4; x += 4)
                    {
                        var (r, g, b) = operation(srcRow[x + 2], srcRow[x + 1], srcRow[x]);
                        dstRow[x] = b;
                        dstRow[x + 1] = g;
                        dstRow[x + 2] = r;
                        dstRow[x + 3] = srcRow[x + 3]; // Preserve Alpha
                    }
                });
            }

            source.UnlockBits(srcData);
            result.UnlockBits(dstData);
            return result;
        }

        private static Bitmap Convolve(Bitmap source, double[,] kernel, double factor)
        {
            int w = source.Width;
            int h = source.Height;
            Bitmap result = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            BitmapData srcData = source.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData dstData = result.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            unsafe
            {
                byte* src = (byte*)srcData.Scan0;
                byte* dst = (byte*)dstData.Scan0;
                int stride = srcData.Stride;

                Parallel.For(1, h - 1, y =>
                {
                    for (int x = 1; x < w - 1; x++)
                    {
                        double r = 0.0, g = 0.0, b = 0.0;

                        for (int ky = -1; ky <= 1; ky++)
                        {
                            for (int kx = -1; kx <= 1; kx++)
                            {
                                int idx = (y + ky) * stride + (x + kx) * 4;
                                double weight = kernel[ky + 1, kx + 1];
                                b += src[idx] * weight;
                                g += src[idx + 1] * weight;
                                r += src[idx + 2] * weight;
                            }
                        }

                        int dstIdx = y * stride + x * 4;
                        dst[dstIdx] = (byte)Math.Clamp(b * factor, 0, 255);
                        dst[dstIdx + 1] = (byte)Math.Clamp(g * factor, 0, 255);
                        dst[dstIdx + 2] = (byte)Math.Clamp(r * factor, 0, 255);
                        dst[dstIdx + 3] = 255;
                    }
                });
            }
            source.UnlockBits(srcData);
            result.UnlockBits(dstData);
            return result;
        }

        private static Bitmap ConvolveMagnitude(Bitmap source, double[,] kx, double[,] ky)
        {
            int w = source.Width;
            int h = source.Height;
            Bitmap result = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            BitmapData srcData = source.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData dstData = result.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            unsafe
            {
                byte* src = (byte*)srcData.Scan0;
                byte* dst = (byte*)dstData.Scan0;
                int stride = srcData.Stride;

                Parallel.For(1, h - 1, y =>
                {
                    for (int x = 1; x < w - 1; x++)
                    {
                        double rx = 0, gx = 0, bx = 0;
                        double ry = 0, gy = 0, by = 0;

                        for (int fY = -1; fY <= 1; fY++)
                        {
                            for (int fX = -1; fX <= 1; fX++)
                            {
                                int idx = (y + fY) * stride + (x + fX) * 4;
                                bx += src[idx] * kx[fY + 1, fX + 1]; by += src[idx] * ky[fY + 1, fX + 1];
                                gx += src[idx + 1] * kx[fY + 1, fX + 1]; gy += src[idx + 1] * ky[fY + 1, fX + 1];
                                rx += src[idx + 2] * kx[fY + 1, fX + 1]; ry += src[idx + 2] * ky[fY + 1, fX + 1];
                            }
                        }

                        int dstIdx = y * stride + x * 4;
                        dst[dstIdx] = (byte)Math.Clamp(Math.Sqrt(bx * bx + by * by), 0, 255);
                        dst[dstIdx + 1] = (byte)Math.Clamp(Math.Sqrt(gx * gx + gy * gy), 0, 255);
                        dst[dstIdx + 2] = (byte)Math.Clamp(Math.Sqrt(rx * rx + ry * ry), 0, 255);
                        dst[dstIdx + 3] = 255;
                    }
                });
            }
            source.UnlockBits(srcData);
            result.UnlockBits(dstData);
            return result;
        }

        private static Bitmap DrawHistogram(int[] hist)
        {
            Bitmap bmp = new Bitmap(256, 150);
            int max = 1;
            foreach (var v in hist) if (v > max) max = v;

            using Graphics g = Graphics.FromImage(bmp);
            g.Clear(Color.White);
            for (int i = 0; i < 256; i++)
            {
                int h = (int)(((double)hist[i] / max) * 150);
                g.DrawLine(Pens.Black, i, 150, i, 150 - h);
            }
            return bmp;
        }
    }
}