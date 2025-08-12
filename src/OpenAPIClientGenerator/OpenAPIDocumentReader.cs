using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace OpenAPIClientGenerator;

public class OpenAPIDocumentReader
{
    private readonly string _contents;

    public OpenAPIDocumentReader(string contents)
    {
        _contents = contents;
    }
    public Task<OpenApiDocument> ReadDocumentAsync()
    {
        OpenApiStringReader reader = new();
        var document = reader.Read(_contents, out var diagnostic);
        if (diagnostic.Errors.Count > 0)
            throw new InvalidOperationException($"Failed to read OpenAPI document: {string.Join(", ", diagnostic.Errors.Select(e => e.Message))}");
        
        return Task.FromResult(document);
    }
}
