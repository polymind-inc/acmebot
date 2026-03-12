namespace Acmebot.App.Options;

public class ExternalAccountBindingOptions
{
    public string KeyId { get; set; }
    public string HmacKey { get; set; }
    public string Algorithm { get; set; } = "HS256";
}
