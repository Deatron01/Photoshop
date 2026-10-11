<h1 align="center">Photoshop</h1>

<p align="center">
  An image editor built for an image-processing course.<br />
  <samp>C# · .NET 8 · WPF · MVVM</samp>
</p>

### <samp>01 — WHAT IT IS</samp>

An image editor written for an image-processing course at Óbuda University. Every
operation is implemented by hand on raw pixel data, with no imaging library doing the
work, and the app reports how each one changed the picture.

The filters and the measurements live in two separate libraries behind an MVVM WPF front
end, so the processing code has no dependency on the UI.

### <samp>02 — OPERATIONS</samp>

| Group | Operations |
|---|---|
| Point operations | Negation, gamma correction, logarithmic transform, grayscale conversion |
| Histogram | Histogram rendering, histogram equalisation |
| Smoothing | Box filter, Gaussian filter |
| Edges and features | Sobel, Laplace, Harris corner detection |

After a filter runs, the analytics library measures the result: basic image statistics,
the share of pixels that are edges, and the number of Harris keypoints.

### <samp>03 — PROJECTS</samp>

| Project | Role |
|---|---|
| `ImageProcessing.Core` | The filters (`ImageProcessor`) |
| `ImageProcessing.Analytics` | Statistics, edge percentage, keypoint counting (`ImageAnalyzer`) |
| `Photoshop` (`ImageProcessing.WpfApp`) | WPF front end: `MainViewModel`, `RelayCommand`, result window |

### <samp>04 — RUN IT</samp>

Requires Windows and the .NET 8 SDK.

```bash
dotnet run --project Photoshop/Photoshop/ImageProcessing.WpfApp.csproj
```

Or open `Photoshop/Photoshop.sln` in Visual Studio 2022.

### <samp>05 — WRITE-UPS</samp>

Both are in Hungarian.

- `Photoshop/Irodalomkutatas.docx`: literature review
- `Photoshop/Projektben_hasznalt_algoritmusok_es_optimalizaciok.docx`: the algorithms used
  and how they were optimised

<br />

<p align="center">
  <a href="https://github.com/Deatron01"><img src="https://raw.githubusercontent.com/Deatron01/Deatron01/main/assets/icon.png" width="56" alt="Dea" /></a><br />
  <sub><i>Little chaos in a quiet mind.</i></sub><br />
  <sub><samp>MORE WORK AT <a href="https://github.com/Deatron01">GITHUB.COM/DEATRON01</a></samp></sub>
</p>
