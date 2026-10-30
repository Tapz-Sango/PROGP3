using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SmartX.Api.Services;

// Authentication service.
// - When Firebase:UseFirebase = true, validates against Firebase Identity Toolkit REST API.
// - Otherwise falls back to a local demo user (so the app runs without keys).
// - ALWAYS requires BOTH email AND password to match (Part 1 feedback fixed).
// - Issues session tokens that every sensitive endpoint validates.

public class AuthService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _http;
    private readonly Dictionary<string, (string Email, DateTime ExpiresUtc)> _tokens = new();

    // Local fallback — used ONLY when Firebase:UseFirebase = false (demo mode).
    private static readonly Dictionary<string, string> LocalUsers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["guest@smartx.io"] = "admin123",
        ["operator@smartx.io"] = "SmartX@2026"
    };

    public AuthService(IConfiguration config, IHttpClientFactory httpFactory)
    {
        _config = config;
        _http = httpFactory.CreateClient();
    }

    private bool UseFirebase => _config.GetValue<bool>("Firebase:UseFirebase");
    private string FirebaseKey => _config.GetValue<string>("Firebase:ApiKey") ?? "";

    //Returns (true, null) if BOTH email and password are correct.[Validation]
    public async Task<(bool Ok, string? Error)> ValidateCredentialsAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return (false, "Email and password are both required.");

        if (UseFirebase)
            return await ValidateAgainstFirebaseAsync(email, password);

        // Local fallback (demo mode) — requires exact match of BOTH fields.
        if (LocalUsers.TryGetValue(email, out var stored) && stored == password)
            return (true, null);

        return (false, "Invalid credentials.");
    }

    private async Task<(bool Ok, string? Error)> ValidateAgainstFirebaseAsync(string email, string password)
    {
        try
        {
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={FirebaseKey}";
            var body = JsonSerializer.Serialize(new { email, password, returnSecureToken = true });
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var res = await _http.PostAsync(url, content);
            if (res.IsSuccessStatusCode) return (true, null);

            var err = await res.Content.ReadAsStringAsync();
            return (false, $"Firebase auth failed: {err}");
        }
        catch (Exception ex)
        {
            return (false, $"Firebase unreachable: {ex.Message}");
        }
    }

    public string IssueToken(string email)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _tokens[token] = (email, DateTime.UtcNow.AddHours(4));
        return token;
    }

    public bool ValidateToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        if (!_tokens.TryGetValue(token, out var entry)) return false;
        if (entry.ExpiresUtc < DateTime.UtcNow)
        {
            _tokens.Remove(token);
            return false;
        }
        return true;
    }

    public void Revoke(string token) => _tokens.Remove(token);
}