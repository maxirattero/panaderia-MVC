namespace Panaderia.Services.Implementations;

public class PushNotificationOptions
{
    public string Subject { get; init; } = string.Empty;
    public string VapidPublicKey { get; init; } = string.Empty;
    public string VapidPrivateKey { get; init; } = string.Empty;

    public bool EstaConfigurado =>
        !string.IsNullOrWhiteSpace(Subject) &&
        !string.IsNullOrWhiteSpace(VapidPublicKey) &&
        !string.IsNullOrWhiteSpace(VapidPrivateKey);
}
