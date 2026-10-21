using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ramai.Api.Workspaces;

public interface IWorkspaceRequestBudget
{
    Task<bool> AllowProvisioningAsync(Guid userId, IPAddress origin, CancellationToken cancellationToken);
}

internal static class WorkspaceHttpInput
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8 };

    internal static async Task<(T? Value, int Error)> Read<T>(HttpContext context) where T : class
    {
        if (!context.Request.HasJsonContentType()) { return (null, 415); }
        // Enforce the same bound on chunked requests/TestServer, not just Content-Length.
        var buffer = new byte[4097];
        var count = 0;
        try
        {
            while (count < buffer.Length)
            {
                var read = await context.Request.Body.ReadAsync(buffer.AsMemory(count), context.RequestAborted);
                if (read == 0) { break; }
                count += read;
            }
            if (count > 4096) { return (null, 413); }
            using var document = JsonDocument.Parse(buffer.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 8 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) { return (null, 400); }
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            { if (!names.Add(property.Name)) { return (null, 400); } }
            var value = document.RootElement.Deserialize<T>(Options);
            return value is null ? (null, 400) : (value, 0);
        }
        catch (JsonException) { return (null, 400); }
        catch (BadHttpRequestException error) { return (null, error.StatusCode == 413 ? 413 : 400); }
    }
}
