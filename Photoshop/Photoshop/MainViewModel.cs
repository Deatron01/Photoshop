using ImageProcessing.Core;
using ImageProcessing.Analytics;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ImageProcessing.WpfApp
{
    public class MainViewModel : ViewModelBase
    {
        private Bitmap? _originalBitmap;
        private BitmapImage? _displayImage;
        private string _executionTime = "Execution Time: 0 ms";
        private double _gammaValue = 1.5;

        public BitmapImage? DisplayImage
        {
            get => _displayImage;
            set { _displayImage = value; OnPropertyChanged(); }
        }

        public string ExecutionTime
        {
            get => _executionTime;
            set { _executionTime = value; OnPropertyChanged(); }
        }

        public double GammaValue
        {
            get => _gammaValue;
            set { _gammaValue = value; OnPropertyChanged(); }
        }

        public ICommand LoadImageCommand { get; }
        public ICommand NegationCommand { get; }
        public ICommand GammaCommand { get; }
        public ICommand LogCommand { get; }
        public ICommand GrayscaleCommand { get; }
        public ICommand HistogramCommand { get; }
        public ICommand HistogramEqCommand { get; }
        public ICommand BoxFilterCommand { get; }
        public ICommand GaussianFilterCommand { get; }
        public ICommand SobelCommand { get; }
        public ICommand LaplaceCommand { get; }
        public ICommand HarrisCommand { get; }

        public MainViewModel()
        {
            LoadImageCommand = new RelayCommand(_ => LoadImage());

            NegationCommand = new RelayCommand(_ => ExecuteFilter(
                "Image Negation", "O(x,y) = 255 - I(x,y)", "None", ImageProcessor.Negate));

            GammaCommand = new RelayCommand(_ => ExecuteFilter(
                "Gamma Correction", "O(x,y) = 255 * (I(x,y)/255)^γ", $"Gamma = {GammaValue}",
                bmp => ImageProcessor.GammaCorrection(bmp, GammaValue),
                analytics => {
                    analytics.CurvePoints = new double[256];
                    for (int i = 0; i < 256; i++) analytics.CurvePoints[i] = Math.Pow(i / 255.0, GammaValue) * 255.0;
                }));

            LogCommand = new RelayCommand(_ => ExecuteFilter(
                "Logarithmic Transform", "O(x,y) = c * log(1 + I(x,y))", "c = 255 / log(256)",
                ImageProcessor.LogTransform,
                analytics => {
                    analytics.CurvePoints = new double[256];
                    double c = 255.0 / Math.Log(256);
                    for (int i = 0; i < 256; i++) analytics.CurvePoints[i] = c * Math.Log(1 + i);
                }));

            GrayscaleCommand = new RelayCommand(_ => ExecuteFilter(
                "Grayscale Conversion", "O(x,y) = 0.299R + 0.587G + 0.114B", "None", ImageProcessor.ToGrayscale));
            HistogramCommand = new RelayCommand(_ => ExecuteFilter(
                    "Image Histogram Analytics",
                    "Intensity Distribution",
                    "None",
                    bmp => (Bitmap)bmp.Clone()
                ));
            HistogramEqCommand = new RelayCommand(_ => ExecuteFilter(
                "Histogram Equalization", "O(x,y) = round( CDF(I(x,y)) / N * 255 )", "None",
                ImageProcessor.HistogramEqualization,
                (analytics, original, result) => {
                    analytics.OriginalHistogram = analytics.BeforeStats.Histogram;
                    analytics.EqualizedHistogram = analytics.AfterStats.Histogram;
                    analytics.CDF = new double[256];
                    int sum = 0;
                    for (int i = 0; i < 256; i++)
                    {
                        sum += analytics.OriginalHistogram[i];
                        analytics.CDF[i] = sum / (double)(original.Width * original.Height);
                    }
                }));

            BoxFilterCommand = new RelayCommand(_ => ExecuteFilter(
                "Box Filter (Blur)", "Convolution (Mean)", "Kernel: 3x3, Factor: 1/9", ImageProcessor.BoxFilter,
                analytics => analytics.KernelMatrix = new double[,] { { 1 / 9.0, 1 / 9.0, 1 / 9.0 }, { 1 / 9.0, 1 / 9.0, 1 / 9.0 }, { 1 / 9.0, 1 / 9.0, 1 / 9.0 } }));

            GaussianFilterCommand = new RelayCommand(_ => ExecuteFilter(
                "Gaussian Filter", "Convolution (Gaussian)", "Kernel: 3x3, Factor: 1/16", ImageProcessor.GaussianFilter,
                analytics => analytics.KernelMatrix = new double[,] { { 1 / 16.0, 2 / 16.0, 1 / 16.0 }, { 2 / 16.0, 4 / 16.0, 2 / 16.0 }, { 1 / 16.0, 2 / 16.0, 1 / 16.0 } }));

            SobelCommand = new RelayCommand(_ => ExecuteFilter(
                "Sobel Edge Detection", "O(x,y) = sqrt(Gx^2 + Gy^2)", "Kernel: 3x3 Sobel", ImageProcessor.Sobel,
                (analytics, original, result) => {
                    analytics.KernelMatrixX = new double[,] { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
                    analytics.KernelMatrixY = new double[,] { { 1, 2, 1 }, { 0, 0, 0 }, { -1, -2, -1 } };
                    analytics.EdgePixelPercentage = ImageAnalyzer.CalculateEdgePercentage(result);
                }));

            LaplaceCommand = new RelayCommand(_ => ExecuteFilter(
                "Laplace Edge Detection", "Convolution (2nd Derivative)", "Kernel: 3x3 Laplace", ImageProcessor.Laplace,
                (analytics, original, result) => {
                    analytics.KernelMatrix = new double[,] { { 0, 1, 0 }, { 1, -4, 1 }, { 0, 1, 0 } };
                    analytics.EdgePixelPercentage = ImageAnalyzer.CalculateEdgePercentage(result);
                }));

            HarrisCommand = new RelayCommand(_ => ExecuteFilter(
                "Harris Corner Detection", "R = Det(M) - k*Trace(M)^2", "k = 0.04, Threshold = 10,000,000", ImageProcessor.HarrisCorners,
                (analytics, original, result) => {
                    analytics.KeypointsDetected = ImageAnalyzer.CountHarrisKeypoints(result);
                    analytics.HarrisThreshold = 10000000;
                }));
        }

        private void LoadImage()
        {
            var dlg = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" };
            if (dlg.ShowDialog() == true)
            {
                _originalBitmap = new Bitmap(dlg.FileName);
                DisplayImage = BitmapToImageSource(_originalBitmap);
            }
        }

        private bool CanExecuteFilter(object? obj) => _originalBitmap != null;

        private void ExecuteFilter(string name, string formula, string parameters, Func<Bitmap, Bitmap> processFunc, Action<OperationAnalytics>? specificPopulator = null)
        {
            ExecuteFilter(name, formula, parameters, processFunc, specificPopulator == null ? null : (Action<OperationAnalytics, Bitmap, Bitmap>)((a, o, r) => specificPopulator(a)));
        }

        private void ExecuteFilter(string name, string formula, string parameters, Func<Bitmap, Bitmap> processFunc, Action<OperationAnalytics, Bitmap, Bitmap>? specificPopulator)
        {
            if (_originalBitmap == null) return;

            var analytics = new OperationAnalytics
            {
                OperationName = name,
                Formula = formula,
                Parameters = parameters,
                BeforeStats = ImageAnalyzer.CalculateStats(_originalBitmap)
            };

            var sw = Stopwatch.StartNew();
            Bitmap resultBmp = processFunc(_originalBitmap);
            sw.Stop();

            analytics.ExecutionTimeMs = sw.ElapsedMilliseconds;
            ExecutionTime = $"Execution Time: {sw.ElapsedMilliseconds} ms";
            analytics.AfterStats = ImageAnalyzer.CalculateStats(resultBmp);

            specificPopulator?.Invoke(analytics, _originalBitmap, resultBmp);

            var resultWindow = new ResultWindow(BitmapToImageSource(resultBmp), analytics);
            resultWindow.Show();
        }

        private static BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using var memory = new MemoryStream();
            bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
            memory.Position = 0;
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }
    }
}