using Microsoft.OpenApi.Models;

namespace OpenAPIClientGenerator;

internal static class Extensions
{
    public static string UpperCaseFirstChar(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        return char.ToUpper(value[0]) + value.Substring(1);
    }

    public static string ToPascalCase(this string name) =>
        string.Concat(name.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries).Select(UpperCaseFirstChar));

    public static string ToHttpMethod(this OperationType operationType)
    {
        return operationType switch
        {
            OperationType.Get => "GET",
            OperationType.Post => "POST",
            OperationType.Put => "PUT",
            OperationType.Delete => "DELETE",
            OperationType.Patch => "PATCH",
            OperationType.Head => "HEAD",
            OperationType.Options => "OPTIONS",
            _ => throw new ArgumentOutOfRangeException(nameof(operationType), operationType, null)
        };
    }
}

