namespace SmartX.Api.Models;

//Email + Password
public record LoginRequest(string Email, string Password);
//Login Response
public record LoginResponse(string UserId, string Email, string Token, DateTime ExpiresUtc);
//Issues
public record IssueRequest(string Description);
//Searching by Term + Category
public record SearchEventRequest(string Term, string Category);
//Commads
public record CommandRequest(string MacAddress, string Command, string PreviousState);
//Ingested Requests
public record TelemetryIngestRequest(string MacAddress, double Value, string Unit, bool IsCritical);