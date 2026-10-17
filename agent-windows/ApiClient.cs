using System.Net.Http.Json;
using System.Text.Json;

namespace Fleetwatch.Agent;

// Thin HTTP wrapper around the Fleetwatch server API.
public sealed class ApiClient
{
    private readonly HttpClient _http;

    public ApiClient(string serverUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(serverUrl.TrimEnd('/') + "/") };
        _http.Timeout = TimeSpan.FromSeconds(60);
    }

    // Sets the device token for all subsequent authenticated calls.
    public void UseDeviceToken(string deviceToken)
    {
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", deviceToken);
    }

    public async Task<EnrollResponse?> EnrollAsync(EnrollRequest req, CancellationToken ct)
    {
        var resp = await _http.PostAsJsonAsync("api/agent/enroll", req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var text = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Enroll failed ({(int)resp.StatusCode}): {text}");
        }
        return await resp.Content.ReadFromJsonAsync<EnrollResponse>(cancellationToken: ct);
    }

    public async Task<CheckinResponse?> CheckinAsync(CheckinRequest req, CancellationToken ct)
    {
        var resp = await _http.PostAsJsonAsync("api/agent/checkin", req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<CheckinResponse>(cancellationToken: ct);
    }

    public async Task ReportResultAsync(string commandId, CommandResult result, CancellationToken ct)
    {
        var resp = await _http.PostAsJsonAsync($"api/agent/commands/{commandId}/result", result, ct);
        resp.EnsureSuccessStatusCode();
    }
}
