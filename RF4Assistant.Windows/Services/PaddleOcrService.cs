using System.IO;
using RapidOCRSharpOnnx;
using RapidOCRSharpOnnx.Inference.PPOCR_Det.Models;
using RapidOCRSharpOnnx.Providers;
using RapidOCRSharpOnnx.Configurations;
using RapidOCRSharpOnnx.Utils;

namespace RF4AssistantPro.Services;

public sealed record PaddleOcrLine(
    string Text,
    double Left,
    double Top,
    double Right,
    double Bottom,
    float Score)
{
    public double CenterX => (Left + Right) / 2.0;
    public double CenterY => (Top + Bottom) / 2.0;
}

public sealed class PaddleOcrService : IDisposable
{
    private readonly object _sync = new();
    private RapidOCRSharp? _ocr;
    private bool _disposed;

    public Task<IReadOnlyList<PaddleOcrLine>> RecognizeAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (_sync)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    var ocr = EnsureOcr();
                    var result = ocr.RecognizeText(imagePath);
                    cancellationToken.ThrowIfCancellationRequested();
                    return ToLines(result.WordResults);
                }
            },
            cancellationToken);
    }

    private RapidOCRSharp EnsureOcr()
    {
        if (_ocr is not null)
        {
            return _ocr;
        }

        var modelRoot = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Ocr",
            "Paddle");
        var detectorPath = RequireModel(
            modelRoot,
            "ch_PP-OCRv5_det_mobile.onnx");
        var recognizerPath = RequireModel(
            modelRoot,
            "cyrillic_PP-OCRv5_rec_mobile.onnx");
        var classifierPath = RequireModel(
            modelRoot,
            "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx");

        var config = new OcrConfig(
            detectorPath,
            recognizerPath,
            LangRec.CYRILLIC,
            OCRVersion.PPOCRV5,
            classifierPath)
        {
            ReturnWordBox = true,
            ReturnSingleCharBox = false
        };
        config.RecognizerConfig.TextScore = 0.35f;
        _ocr = new RapidOCRSharp(new ExecutionProviderCPU(config));
        AppLog.Info(
            "PaddleOCR/ONNX инициализирован: PP-OCRv5, кириллица, CPU.");
        return _ocr;
    }

    private static string RequireModel(string root, string name)
    {
        var path = Path.Combine(root, name);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Не найдена модель PaddleOCR: {path}",
                path);
        }

        return path;
    }

    private static IReadOnlyList<PaddleOcrLine> ToLines(
        IReadOnlyList<DetBoxItem>? items)
    {
        if (items is null)
        {
            return [];
        }

        return items
            .Where(item =>
                item is not null &&
                item.Score >= 0.25f &&
                !string.IsNullOrWhiteSpace(item.Word))
            .Select(item =>
            {
                var left = item.Box.Min(point => point.X);
                var top = item.Box.Min(point => point.Y);
                var right = item.Box.Max(point => point.X);
                var bottom = item.Box.Max(point => point.Y);
                return new PaddleOcrLine(
                    Normalize(item.Word),
                    left,
                    top,
                    right,
                    bottom,
                    item.Score);
            })
            .Where(line => line.Text.Length > 0)
            .ToList();
    }

    private static string Normalize(string text)
    {
        return string.Join(
            " ",
            text.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _ocr?.Dispose();
            _ocr = null;
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}