using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZeroInstall.Core.Models;

namespace ZeroInstall.CLI.Infrastructure;

internal class DashboardClient : IDisposable
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public DashboardClient(string baseUrl, string apiKey)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        _http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    public async Task PushJobAsync(MigrationJob job, CancellationToken ct = default)
    {
        try
        {
            await _http.PostAsJsonAsync("api/jobs", job, JsonOptions, ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to push job to dashboard: {ex.Message}");
        }
    }

    public async Task PushReportAsync(JobReport report, CancellationToken ct = default)
    {
        try
        {
            await _http.PostAsJsonAsync("api/reports", report, JsonOptions, ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to push report to dashboard: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}
