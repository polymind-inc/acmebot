namespace Acmebot.Cli;

internal static class ExitCodes
{
    public const int Success = 0;
    public const int Usage = 2;
    public const int ApiError = 3;
    public const int AuthenticationError = 4;
    public const int NetworkError = 5;
    public const int Canceled = 130;
}
