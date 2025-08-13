using Microsoft.OpenApi.Models;

namespace OpenAPIClientGenerator;

internal class OpenAPIDocumentOperationMethodDefinition
{
    public string PathName { get; set; } = null!;
    public OperationType OperationName { get; set; }
    public string MethodName { get; set; } = null!;
    public string MethodContent { get; set; } = null!;

    public override int GetHashCode()
    {
        return MethodContent.GetHashCode();
    }
}
