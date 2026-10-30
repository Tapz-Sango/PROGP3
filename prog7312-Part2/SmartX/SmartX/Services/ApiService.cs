using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using SmartX.Models;

namespace SmartX.Services
{
    public class ApiService
    {
        private readonly HttpClient _http;
        private const string BaseUrl = "http://localhost:5245";

        public ApiService()
        {
            _http = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(15) };
        }

        private void AttachAuth()
        {
            _http.DefaultRequestHeaders.Authorization = AuthSession.IsAuthenticated
                ? new AuthenticationHeaderValue("Bearer", AuthSession.Token)
                : null;
        }

        //  AUTH 
        public async Task<(bool Ok, string? Error)> AuthenticateAsync(string email, string password)
        {
            try
            {
                var res = await _http.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
                if (!res.IsSuccessStatusCode)
                {
                    var err = await res.Content.ReadAsStringAsync();
                    return (false, err);
                }
                var payload = await res.Content.ReadFromJsonAsync<LoginResponse>();
                if (payload == null) return (false, "Empty response");
                AuthSession.Token = payload.Token;
                AuthSession.Email = payload.Email;
                AuthSession.ExpiresUtc = payload.ExpiresUtc;
                return (true, null);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public void Logout()
        {
            AuthSession.Token = null;
            AuthSession.Email = null;
        }

        //  DEVICES 
        public async Task<List<Device>> GetDevicesAsync()
        {
            try { AttachAuth(); return await _http.GetFromJsonAsync<List<Device>>("/api/devices") ?? new(); }
            catch { return new(); }
        }

        public async Task<bool> RegisterSensorAsync(object payload)
        {
            try { AttachAuth(); return (await _http.PostAsJsonAsync("/api/sensors", payload)).IsSuccessStatusCode; }
            catch { return false; }
        }

        //  TELEMETRY 
        public async Task<bool> IngestTelemetryAsync(string mac, double value, string unit, bool isCritical = false)
        {
            try
            {
                AttachAuth();
                return (await _http.PostAsJsonAsync("/api/telemetry/ingest",
                    new { MacAddress = mac, Value = value, Unit = unit, IsCritical = isCritical })).IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<List<TelemetryPoint>> GetTimelineAsync(string? mac = null, int take = 50)
        {
            try
            {
                AttachAuth();
                var url = string.IsNullOrWhiteSpace(mac)
                    ? $"/api/telemetry/timeline?take={take}"
                    : $"/api/telemetry/timeline?mac={Uri.EscapeDataString(mac)}&take={take}";
                return await _http.GetFromJsonAsync<List<TelemetryPoint>>(url) ?? new();
            }
            catch { return new(); }
        }

        public async Task<QueueSnapshot?> GetQueueSnapshotAsync()
        {
            try { AttachAuth(); return await _http.GetFromJsonAsync<QueueSnapshot>("/api/telemetry/queue"); }
            catch { return null; }
        }

        //  COMMANDS 
        public async Task<bool> IssueCommandAsync(string mac, string command, string previousState)
        {
            try
            {
                AttachAuth();
                return (await _http.PostAsJsonAsync("/api/commands",
                    new { MacAddress = mac, Command = command, PreviousState = previousState })).IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<UndoResult?> UndoLastCommandAsync()
        {
            try
            {
                AttachAuth();
                var res = await _http.PostAsync("/api/commands/undo", null);
                if (!res.IsSuccessStatusCode) return null;
                return await res.Content.ReadFromJsonAsync<UndoResult>();
            }
            catch { return null; }
        }

        public async Task<List<CommandRecord>> GetCommandHistoryAsync()
        {
            try { AttachAuth(); return await _http.GetFromJsonAsync<List<CommandRecord>>("/api/commands/history") ?? new(); }
            catch { return new(); }
        }

        //  PREDICTIVE 
        public async Task<List<Prediction>> LogSearchAsync(string term, string category)
        {
            try
            {
                AttachAuth();
                var res = await _http.PostAsJsonAsync("/api/search", new { Term = term, Category = category });
                if (!res.IsSuccessStatusCode) return new();
                var wrapper = await res.Content.ReadFromJsonAsync<SearchWrapper>();
                return wrapper?.Suggestions ?? new();
            }
            catch { return new(); }
        }

        public async Task<List<Prediction>> GetInsightsAsync()
        {
            try { AttachAuth(); return await _http.GetFromJsonAsync<List<Prediction>>("/api/insights") ?? new(); }
            catch { return new(); }
        }

        //  FILES 
        public async Task<(bool Ok, string Message, int Imported)> UploadDeviceFileAsync(string mac, string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return (false, "File not found.", 0);
                AttachAuth();

                using var content = new MultipartFormDataContent();
                using var fs = File.OpenRead(filePath);
                using var sc = new StreamContent(fs);
                content.Add(sc, "file", Path.GetFileName(filePath));

                var res = await _http.PostAsync($"/api/sensors/{mac}/upload", content);
                var body = await res.Content.ReadAsStringAsync();
                if (!res.IsSuccessStatusCode) return (false, body, 0);

                using var doc = JsonDocument.Parse(body);
                int imported = doc.RootElement.TryGetProperty("devicesImported", out var imp) ? imp.GetInt32() : 0;
                return (true, body, imported);
            }
            catch (Exception ex) { return (false, ex.Message, 0); }
        }

        public async Task<bool> PostIssueAsync(string description)
        {
            try { AttachAuth(); return (await _http.PostAsJsonAsync("/api/issues", new { Description = description })).IsSuccessStatusCode; }
            catch { return false; }
        }

        //  DTOs 
        private class LoginResponse
        {
            public string UserId { get; set; } = "";
            public string Email { get; set; } = "";
            public string Token { get; set; } = "";
            public DateTime ExpiresUtc { get; set; }
        }
        private class SearchWrapper { public List<Prediction> Suggestions { get; set; } = new(); }
    }

    public class TelemetryPoint
    {
        public string MacAddress { get; set; } = "";
        public double Value { get; set; }
        public string Unit { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public bool IsCritical { get; set; }
    }

    public class QueueSnapshot
    {
        public List<object>? Standard { get; set; }
        public List<object>? Critical { get; set; }
    }

    public class UndoResult
    {
        public string Message { get; set; } = "";
        public CommandRecord? Undone { get; set; }
    }
}