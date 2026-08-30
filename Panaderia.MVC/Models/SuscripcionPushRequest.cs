namespace Panaderia.MVC.Models;

public class SuscripcionPushRequest
{
    public string? Endpoint { get; set; }
    public SuscripcionPushKeysRequest? Keys { get; set; }
}

public class SuscripcionPushKeysRequest
{
    public string? P256dh { get; set; }
    public string? Auth { get; set; }
}
