using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace OpenAPIClientGenerator.DocumentReaders;

public class HttpOpenAPIDocumentReader(string uri) : IOpenApiDocumentReader
{
    public async Task<OpenApiDocument> ReadDocumentAsync()
    {
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(uri);
        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync();

        var reader = new OpenApiStreamReader();
        var result = await reader.ReadAsync(stream);

        if (result.OpenApiDiagnostic.Errors.Count > 0)
            throw new InvalidOperationException($"Failed to read OpenAPI document: {string.Join(", ", result.OpenApiDiagnostic.Errors.Select(e => e.Message))}");
        
        return result.OpenApiDocument;
    }
}
