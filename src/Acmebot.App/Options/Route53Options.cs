namespace Acmebot.App.Options;

public class Route53Options
{
    public string? RoleArn { get; set; }
    public string? ManagedIdentityClientId { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
}
