namespace SmartX.Services;

// Static holder so every view can read the current token. Also works if firebasse is offline.
public static class AuthSession
{
    public static string? Token { get; set; }
    public static string? Email { get; set; }
    public static DateTime ExpiresUtc { get; set; }
    public static bool IsAuthenticated => !string.IsNullOrEmpty(Token) && ExpiresUtc > DateTime.UtcNow;
}