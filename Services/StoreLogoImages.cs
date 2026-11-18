using SkiaSharp;

namespace AdGestionHub.Services;

public static class StoreLogoImages
{
    public const int MaxUploadBytes = 2 * 1024 * 1024;
    public const int MaxFormBytes = MaxUploadBytes + 128 * 1024;
    private const int MaxInputEdge = 2048;
    private const int MaxOutputEdge = 512;
    private const string InvalidImage = "Le logo doit être une image PNG ou JPEG valide et non animée.";

    public static async Task<byte[]> NormalizeAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length < 8 || file.Length > MaxUploadBytes)
            throw new CommerceRuleException("Choisissez une image PNG ou JPEG de 2 Mo maximum.");

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, cancellationToken);
        var header = buffer.GetBuffer().AsSpan(0, 8);
        if (!header.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && !header[..3].SequenceEqual(new byte[] { 255, 216, 255 }))
            throw new CommerceRuleException(InvalidImage);

        buffer.Position = 0;
        using var codec = SKCodec.Create(buffer);
        if (codec == null || codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg))
            throw new CommerceRuleException(InvalidImage);
        if (codec.Info.Width is <= 0 or > MaxInputEdge || codec.Info.Height is <= 0 or > MaxInputEdge)
            throw new CommerceRuleException("Le logo doit mesurer au maximum 2 048 pixels par côté.");
        if (codec.FrameCount > 1) throw new CommerceRuleException(InvalidImage);

        var alpha = codec.Info.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul;
        var sourceInfo = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, alpha);
        using var bitmap = new SKBitmap(sourceInfo);
        if (codec.GetPixels(sourceInfo, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new CommerceRuleException(InvalidImage);

        // Resize before storing, decode/re-encode even small files: no original payload, name, or metadata is retained.
        var scale = Math.Min(1d, (double)MaxOutputEdge / Math.Max(sourceInfo.Width, sourceInfo.Height));
        using var resized = scale < 1d
            ? bitmap.Resize(new SKSizeI(Math.Max(1, (int)(scale * sourceInfo.Width)), Math.Max(1, (int)(scale * sourceInfo.Height))),
                new SKSamplingOptions(SKFilterMode.Linear))
            : null;
        if (scale < 1d && resized == null) throw new CommerceRuleException(InvalidImage);
        using var encoded = (resized ?? bitmap).Encode(SKEncodedImageFormat.Png, 100);
        return encoded?.ToArray() ?? throw new CommerceRuleException(InvalidImage);
    }

    public static string DataUrl(byte[] png) => $"data:image/png;base64,{Convert.ToBase64String(png)}";
}
