using Microsoft.CodeAnalysis;
using Microsoft.OpenApi.Models;

namespace OpenAPIClientGenerator;

internal class APIDocumentSourceData(ISymbol symbol, OpenApiDocument apiDocument)
{
    public ISymbol ClassSymbol { get; private set; } = symbol;
    public OpenApiDocument OpenAPIDocument { get; private set; } = apiDocument;
}