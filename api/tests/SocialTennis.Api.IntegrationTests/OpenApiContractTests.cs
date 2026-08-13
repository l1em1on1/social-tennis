using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SocialTennis.Api.IntegrationTests;

/// <summary>
/// Assertions about the published contract itself rather than any one endpoint.
/// These test the generated OpenAPI document — the actual wire surface, and the
/// input the TS client is built from — so a violation is caught however it was
/// produced in C#.
/// </summary>
public class OpenApiContractTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] HttpVerbs =
        ["get", "put", "post", "delete", "patch", "head", "options", "trace"];

    private async Task<JsonDocument> GetDocumentAsync()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OpenApi_document_is_served()
    {
        using var document = await GetDocumentAsync();

        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/clubs", out _));
    }

    /// <summary>
    /// ADR-0012: list endpoints return an envelope, never a bare array, so that
    /// list-level facts (counts, paging) can be added without a breaking change.
    /// </summary>
    /// <remarks>
    /// This is the only thing enforcing that rule. A base record or marker
    /// interface can make envelopes uniform but cannot compel an endpoint to use
    /// one — nothing stops a new handler returning List&lt;T&gt; directly. Asserting
    /// against the document catches it whatever the C# looks like.
    /// </remarks>
    [Fact]
    public async Task No_endpoint_returns_a_bare_array()
    {
        using var document = await GetDocumentAsync();

        var offenders = new List<string>();

        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (!HttpVerbs.Contains(operation.Name) ||
                    !operation.Value.TryGetProperty("responses", out var responses))
                {
                    continue;
                }

                foreach (var response in responses.EnumerateObject())
                {
                    if (!response.Name.StartsWith('2') ||
                        !response.Value.TryGetProperty("content", out var content) ||
                        !content.TryGetProperty("application/json", out var json) ||
                        !json.TryGetProperty("schema", out var schema) ||
                        !IsArray(schema))
                    {
                        continue;
                    }

                    offenders.Add($"{operation.Name.ToUpperInvariant()} {path.Name} -> {response.Name}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"These responses are a bare array; wrap each in an envelope (ADR-0012): {string.Join(", ", offenders)}");
    }

    // OpenAPI 3.1 permits "type" to be either a string or an array of strings,
    // and .NET emits both forms — an int32 comes through as ["integer","string"].
    private static bool IsArray(JsonElement schema) =>
        schema.TryGetProperty("type", out var type) && type.ValueKind switch
        {
            JsonValueKind.String => type.ValueEquals("array"),
            JsonValueKind.Array => type.EnumerateArray().Any(t => t.ValueEquals("array")),
            _ => false,
        };
}
