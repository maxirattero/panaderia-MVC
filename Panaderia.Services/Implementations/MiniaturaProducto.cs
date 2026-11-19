using SkiaSharp;

namespace Panaderia.Services.Implementations;

internal static class MiniaturaProducto
{
    private const int LadoMaximo = 160;
    private const long PixelesMaximos = 24_000_000;

    public static byte[] Generar(byte[] datos)
    {
        using var entrada = SKData.CreateCopy(datos);
        using var codec = SKCodec.Create(entrada);
        if (codec == null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
            return [];

        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > PixelesMaximos)
            return [];

        var escala = Math.Min(1f, (float)LadoMaximo / Math.Max(info.Width, info.Height));
        var tamanioDecodificado = codec.GetScaledDimensions(escala);
        using var original = SKBitmap.Decode(codec, new SKImageInfo(tamanioDecodificado.Width, tamanioDecodificado.Height));
        if (original == null) return [];

        var ancho = Math.Max(1, (int)Math.Round(info.Width * escala));
        var alto = Math.Max(1, (int)Math.Round(info.Height * escala));
        var orientacion = codec.EncodedOrigin;
        var gira = orientacion is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        using var miniatura = new SKBitmap(gira ? alto : ancho, gira ? ancho : alto);
        using var lienzo = new SKCanvas(miniatura);
        lienzo.Clear(SKColors.Transparent);
        var transformacion = orientacion switch
        {
            SKEncodedOrigin.TopRight => SKMatrix.CreateScale(-1, 1, ancho / 2f, 0),
            SKEncodedOrigin.BottomRight => SKMatrix.CreateScale(-1, -1, ancho / 2f, alto / 2f),
            SKEncodedOrigin.BottomLeft => SKMatrix.CreateScale(1, -1, 0, alto / 2f),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, alto, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, alto, -1, 0, ancho, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, ancho, 0, 0, 1),
            _ => SKMatrix.Identity
        };
        lienzo.SetMatrix(transformacion);
        lienzo.DrawBitmap(original, new SKRect(0, 0, ancho, alto), new SKSamplingOptions(SKFilterMode.Linear));
        using var webp = miniatura.Encode(SKEncodedImageFormat.Webp, 78);
        return webp?.ToArray() ?? [];
    }
}
