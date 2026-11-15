namespace SocialTennis.Api;

/// <summary>
/// `dotnet SocialTennis.Api.dll healthcheck`: the container's own probe of
/// /health/ready, for Docker's HEALTHCHECK in compose (#25). The runtime image
/// is chiseled, with no shell, curl or wget, so the probe is the app itself.
/// It builds no host: one request, then exit 0 (ready) or 1 (anything else).
/// Azure Container Apps probes over HTTP and doesn't use it.
/// </summary>
public static class HealthProbe
{
    public static async Task<int> RunAsync()
    {
        // The port the image listens on (ASPNETCORE_URLS in api/Dockerfile).
        var port = Environment.GetEnvironmentVariable("HEALTHCHECK_PORT") ?? "8080";
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        try
        {
            using var response = await http.GetAsync(new Uri($"http://127.0.0.1:{port}/health/ready"));
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }
}
