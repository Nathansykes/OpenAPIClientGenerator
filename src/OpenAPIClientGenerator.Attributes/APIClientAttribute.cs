namespace OpenAPIClientGenerator.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class APIClientAttribute : Attribute
{
    public string OpenApiPath { get; }
    public APIClientAttribute(string openApiPath)
    {
        OpenApiPath = openApiPath;
    }
}
