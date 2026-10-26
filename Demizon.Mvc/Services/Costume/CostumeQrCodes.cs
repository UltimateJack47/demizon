using Demizon.Common.Configuration;
using Microsoft.Extensions.Options;
using QRCoder;

namespace Demizon.Mvc.Services.Costume;

/// <summary>Obsah a obrázky QR kódů krojárky. Formát URL viz <see cref="CostumeSettings.PublicBaseUrl"/>.</summary>
public sealed class CostumeQrCodes(IOptions<CostumeSettings> options)
{
    public const string ItemPath = "k";
    public const string MemberPath = "m";

    private string BaseUrl => options.Value.PublicBaseUrl.TrimEnd('/');

    public string ItemUrl(string code) => $"{BaseUrl}/{ItemPath}/{Uri.EscapeDataString(code)}";

    public string MemberUrl(string token) => $"{BaseUrl}/{MemberPath}/{Uri.EscapeDataString(token)}";

    /// <summary>SVG pro tisk štítků — škáluje se bez rozmazání.</summary>
    public static string Svg(string content) =>
        new SvgQRCode(Generate(content)).GetGraphic(4, "#000000", "#ffffff", drawQuietZones: true);

    /// <summary>PNG pro mobil (Flutter ho jen zobrazí, nepotřebuje kvůli tomu balíček).</summary>
    public static byte[] Png(string content) => new PngByteQRCode(Generate(content)).GetGraphic(10);

    // Úroveň M: štítek přežije menší ušpinění nebo seprání a QR zůstane malý.
    private static QRCodeData Generate(string content) =>
        new QRCodeGenerator().CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
}
