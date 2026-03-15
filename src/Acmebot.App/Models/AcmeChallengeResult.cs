namespace Acmebot.App.Models;

public class AcmeChallengeResult
{
    public required Uri Url { get; set; }
    public required string DnsRecordName { get; set; }
    public required string DnsRecordValue { get; set; }
}
