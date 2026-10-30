using Microsoft.AspNetCore.Mvc;
using SmartX.Api.DataStructures;
using SmartX.Api.Models;
using SmartX.Api.Services;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<EncryptionService>();
builder.Services.AddSingleton<DeviceRegistry>();
builder.Services.AddSingleton<TelemetryQueue>();
builder.Services.AddSingleton<CriticalAlertQueue>();
builder.Services.AddSingleton<CommandUndoStack>();
builder.Services.AddSingleton<SensorTimeline>();
builder.Services.AddSingleton<DisconnectedNodes>();
builder.Services.AddSingleton<PredictiveEngine>();
builder.Services.AddCors(o => o.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");

var auth = app.Services.GetRequiredService<AuthService>();
var encryption = app.Services.GetRequiredService<EncryptionService>();
var devices = app.Services.GetRequiredService<DeviceRegistry>();
var telemetryQueue = app.Services.GetRequiredService<TelemetryQueue>();
var criticalQueue = app.Services.GetRequiredService<CriticalAlertQueue>();
var undoStack = app.Services.GetRequiredService<CommandUndoStack>();
var timeline = app.Services.GetRequiredService<SensorTimeline>();
var disconnected = app.Services.GetRequiredService<DisconnectedNodes>();
var predictive = app.Services.GetRequiredService<PredictiveEngine>();

SeedDemoDevices(devices);

//  AUTH 
app.MapPost("/api/auth/login", async ([FromBody] LoginRequest req) =>
{
    var (ok, err) = await auth.ValidateCredentialsAsync(req.Email, req.Password);
    if (!ok) return Results.Json(new { message = err ?? "Invalid credentials" }, statusCode: 401);

    var token = auth.IssueToken(req.Email);
    return Results.Ok(new LoginResponse(Guid.NewGuid().ToString(), req.Email, token, DateTime.UtcNow.AddHours(4)));
});

bool IsAuthed(HttpRequest r)
    => auth.ValidateToken(r.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", ""));

//  DEVICES 
app.MapGet("/api/devices", (HttpRequest r) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    return Results.Ok(devices.All.OrderBy(d => d.Name));
});

app.MapGet("/api/devices/filter", (HttpRequest r, string? category, string? alertState, string? search) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    IEnumerable<DeviceRecord> result = devices.FilterByCategory(category);
    if (!string.IsNullOrWhiteSpace(alertState) && !alertState.Equals("All", StringComparison.OrdinalIgnoreCase))
        result = result.Where(d => d.AlertState.Equals(alertState, StringComparison.OrdinalIgnoreCase));
    if (!string.IsNullOrWhiteSpace(search))
        result = result.Where(d =>
            d.MacAddress.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            d.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            d.Location.Contains(search, StringComparison.OrdinalIgnoreCase));

    return Results.Ok(result.OrderBy(d => d.Name));
});

app.MapGet("/api/devices/{mac}", (HttpRequest r, string mac) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    return devices.TryGet(mac, out var d) ? Results.Ok(d) : Results.NotFound();
});

//  SENSOR REGISTRATION 
app.MapPost("/api/sensors", (HttpRequest r, [FromBody] DeviceRecord dto) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    if (string.IsNullOrEmpty(dto.MacAddress) || !Regex.IsMatch(dto.MacAddress, "^[0-9A-Fa-f]{2}(:[0-9A-Fa-f]{2}){5}$"))
        return Results.BadRequest(new { message = "Invalid MAC format. Expected XX:XX:XX:XX:XX:XX" });

    var saved = devices.Upsert(dto);
    return Results.Created($"/api/devices/{saved.MacAddress}", saved);
});

//  FILE UPLOAD + AUTO-IMPORT 
app.MapPost("/api/sensors/{mac}/upload", async (HttpRequest r, string mac, IFormFile file) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    var (ok, err) = encryption.ValidateFile(file);
    if (!ok) return Results.BadRequest(new { message = err });

    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
    var encPath = await encryption.EncryptToDiskAsync(file, uploadsFolder);

    int imported = 0;
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (ext == ".txt" || ext == ".csv")
    {
        using var reader = new StreamReader(file.OpenReadStream());
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            var parts = line.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length >= 4 && Regex.IsMatch(parts[0], "^[0-9A-Fa-f]{2}(:[0-9A-Fa-f]{2}){5}$"))
            {
                devices.Upsert(new DeviceRecord
                {
                    MacAddress = parts[0],
                    Name = parts[1],
                    Location = parts[2],
                    Category = parts[3]
                });
                imported++;
            }
        }
    }

    return Results.Ok(new
    {
        status = "Encrypted & Stored",
        fileName = file.FileName,
        path = encPath,
        devicesImported = imported
    });
}).DisableAntiforgery();

// TELEMETRY INGEST 
app.MapPost("/api/telemetry/ingest", (HttpRequest r, [FromBody] TelemetryIngestRequest t) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();

    var packet = new TelemetryPacketRecord
    {
        MacAddress = t.MacAddress,
        Value = t.Value,
        Unit = t.Unit,
        Timestamp = DateTime.UtcNow
    };

    bool critical = t.IsCritical;
    devices.TryGet(t.MacAddress, out var dev);
    if (dev != null)
    {
        var cat = dev.Category ?? "";
        if (cat.Contains("Moisture", StringComparison.OrdinalIgnoreCase) && t.Value < 20) critical = true;
        if (cat.Contains("Power", StringComparison.OrdinalIgnoreCase) && t.Value > 2000) critical = true;
        if (cat.Contains("Temperature", StringComparison.OrdinalIgnoreCase) && t.Value > 35) critical = true;
    }

    packet.IsCritical = critical;
    var alertState = critical ? "Critical" : "Normal";
    devices.UpdateReading(t.MacAddress, t.Value, t.Unit, alertState);
    timeline.Add(packet);

    if (critical)
    {
        var reason = $"Value {t.Value}{t.Unit} out of range";
        if (disconnected.MarkActive(t.MacAddress, reason))
        {
            criticalQueue.Enqueue(new CriticalAlert
            {
                MacAddress = t.MacAddress,
                Message = $"{dev?.Name ?? t.MacAddress}: {reason}",
                Severity = "Critical"
            }, AlertUrgency.Critical);
        }
    }
    else
    {
        telemetryQueue.Enqueue(packet);
        // When a reading returns to normal, clear the alert gate so a fresh spike can fire a new alert.
        disconnected.Clear(t.MacAddress, $"Value {t.Value}{t.Unit} out of range");
    }

    return Results.Ok(new
    {
        queued = critical ? "CRITICAL" : "STANDARD",
        queueDepth = telemetryQueue.Count,
        alertDepth = criticalQueue.Count
    });
});

app.MapGet("/api/telemetry/queue", (HttpRequest r) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    return Results.Ok(new
    {
        standard = telemetryQueue.PeekAll(),
        critical = criticalQueue.PeekAll()
    });
});

app.MapGet("/api/telemetry/timeline", (HttpRequest r, string? mac, int take = 50) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    var result = string.IsNullOrWhiteSpace(mac) ? timeline.Latest(take) : timeline.ForDevice(mac, take);
    return Results.Ok(result);
});

app.MapPost("/api/telemetry/process-next", (HttpRequest r) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    if (criticalQueue.TryDequeue(out var crit)) return Results.Ok(new { processed = "CRITICAL", packet = crit });
    if (telemetryQueue.TryDequeue(out var normal)) return Results.Ok(new { processed = "STANDARD", packet = normal });
    return Results.Ok(new { processed = "NONE", packet = (object?)null });
});

// COMMANDS + UNDO 
app.MapPost("/api/commands", (HttpRequest r, [FromBody] CommandRequest req) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    var rec = new CommandRecord
    {
        MacAddress = req.MacAddress,
        DeviceName = devices.TryGet(req.MacAddress, out var d) ? d.Name : req.MacAddress,
        Command = req.Command,
        PreviousState = req.PreviousState
    };
    undoStack.Push(rec);
    predictive.RecordEvent($"command:{req.MacAddress}:{req.Command}");

    return Results.Ok(new { message = $"Command '{req.Command}' queued.", stackDepth = undoStack.Count, record = rec });
});

app.MapPost("/api/commands/undo", (HttpRequest r) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    // Never crashes: TryUndo returns false when empty.
    if (!undoStack.TryUndo(out var undone))
        return Results.Ok(new { message = "Nothing to undo.", undone = (object?)null });

    predictive.RecordEvent($"undo:{undone!.MacAddress}");
    return Results.Ok(new { message = $"Undid '{undone.Command}' on {undone.DeviceName}.", undone });
});

app.MapGet("/api/commands/history", (HttpRequest r) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    return Results.Ok(undoStack.History());
});

//  ALERTS 
app.MapGet("/api/alerts/active", (HttpRequest r) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    return Results.Ok(disconnected.All());
});

//  PREDICTIVE 
app.MapPost("/api/search", (HttpRequest r, [FromBody] SearchEventRequest req) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    predictive.RecordEvent($"search:{req.Term}");
    if (!string.IsNullOrWhiteSpace(req.Category))
        predictive.RecordEvent($"category:{req.Category}");

    var suggestions = predictive.GetSuggestions($"search:{req.Term}");
    return Results.Ok(new { suggestions });
});

app.MapGet("/api/insights", (HttpRequest r) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    return Results.Ok(predictive.HighlightProblemDevices(devices.All));
});

//  ISSUES 
var issues = new List<string>();
app.MapGet("/api/issues", (HttpRequest r) => IsAuthed(r) ? Results.Ok(issues) : Results.Unauthorized());
app.MapPost("/api/issues", (HttpRequest r, [FromBody] IssueRequest req) =>
{
    if (!IsAuthed(r)) return Results.Unauthorized();
    issues.Add($"[{DateTime.UtcNow:HH:mm:ss}] {req.Description}");
    return Results.Ok(new { message = "Issue logged." });
});

app.Run();

static void SeedDemoDevices(DeviceRegistry devices)
{
    var now = DateTime.UtcNow;
    devices.Upsert(new DeviceRecord { MacAddress = "A4:C1:38:9B:4F:10", Name = "Moisture Sensor B", Location = "Hydroponic Zone B", Category = "Moisture", LastValue = 45.2, LastUnit = "%", LastSeen = now });
    devices.Upsert(new DeviceRecord { MacAddress = "C8:2B:96:11:02:AA", Name = "Utility Grid Meter 1", Location = "Utility Room", Category = "Power", LastValue = 820, LastUnit = "W", LastSeen = now });
    devices.Upsert(new DeviceRecord { MacAddress = "1C:46:79:A5:5C:E5", Name = "Pump A", Location = "Hydroponic Zone A", Category = "Actuator", LastValue = 0, LastUnit = "STATE", LastSeen = now });
    devices.Upsert(new DeviceRecord { MacAddress = "AD:76:E3:5F:59:93", Name = "Temp Sensor C", Location = "Greenhouse 3", Category = "Temperature", LastValue = 22.4, LastUnit = "°C", LastSeen = now });
    devices.Upsert(new DeviceRecord { MacAddress = "BF:31:42:27:DF:47", Name = "Relay West", Location = "Utility Room", Category = "Actuator", LastValue = 0, LastUnit = "STATE", LastSeen = now });
}