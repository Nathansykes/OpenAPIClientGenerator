using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text;

namespace OpenAPIClientGenerator;
internal class APIDocumentHandler(SourceProductionContext context, APIDocumentSourceData data)
{
    private readonly HashSet<OpenAPIDocumentOperationMethodDefinition> operations = [];

    private string namespaceName = data.ClassSymbol.ContainingNamespace.ToDisplayString();
    private string apiClientClassName = data.ClassSymbol.Name;

    internal void GenerateCode()
    {
        GenerateApiResponseClasses();
        foreach (KeyValuePair<string, OpenApiPathItem> path in data.OpenAPIDocument.Paths)
        {
            foreach (KeyValuePair<OperationType, OpenApiOperation> operation in path.Value.Operations)
            {
                GenerateOperationMethod(path, operation);
            }
        }

        GenerateApiModels();
        GenerateApiClient();
    }

    private void GenerateApiModels()
    {
        //Debugger.Launch();
        var enums = _generatedEnums.Select(model => ParseTextToEnumDefinition(model.Value)!).ToArray();
        var modelClasses = _generatedModels.Select(model => ParseTextToClassDeclaration(model.Value)!).ToArray();

        var nsDecl = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(namespaceName))
            .AddMembers(enums).AddMembers(modelClasses);

        var formattedCode = nsDecl.NormalizeWhitespace().ToFullString();

        context.AddSource($"ApiModels.g.cs", formattedCode);
    }

    private void GenerateApiClient()
    {
        var methods = operations.Select(op => SyntaxFactory.ParseMemberDeclaration(op.MethodContent)!);

        var classDecl = SyntaxFactory.ClassDeclaration(apiClientClassName)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.PartialKeyword));

        foreach (var method in methods)
        {
            classDecl = classDecl.AddMembers(method);
        }

        var nsDecl = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(namespaceName))
            .AddMembers(classDecl);

        var formattedCode = nsDecl.NormalizeWhitespace().ToFullString();

        context.AddSource($"{apiClientClassName}.g.cs", formattedCode);
    }

    private void GenerateApiResponseClasses()
    {
        string apiResponseClassContent = $$"""
            public partial class ApiResponse
            {
                protected ApiResponse() { }

                public static Task<ApiResponse> CreateAsync(System.Net.Http.HttpResponseMessage response)
                {
                    var apiResponse = new ApiResponse()
                    {
                        HttpResponseMessage = response
                    };
                    return Task.FromResult(apiResponse);
                }

                public System.Net.Http.HttpResponseMessage HttpResponseMessage { get; private set; } = null!;
                public System.Net.HttpStatusCode StatusCode => HttpResponseMessage.StatusCode;
                public void EnsureSuccessStatusCode() => HttpResponseMessage.EnsureSuccessStatusCode();
            }
            """;

        string apiResponseTClassContent = $$"""
            public partial class ApiResponse<T> : ApiResponse
            {
                protected ApiResponse() { }
                public static async Task<ApiResponse<T>> CreateAsync(System.Net.Http.HttpResponseMessage response, System.Text.Json.JsonSerializerOptions jsonSerializerOptions)
                {
                    string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var apiResponse = new ApiResponse<T>
                    {
                        Data = System.Text.Json.JsonSerializer.Deserialize<T>(content, jsonSerializerOptions)
                    };

                    return apiResponse;
                }

                public T Data { get; private set; }
            }
            """;

        string fileApiResponseClassContent = $$"""
            public partial class FileApiResponse : ApiResponse
            {
                protected FileApiResponse() { }
                public static new async Task<FileApiResponse> CreateAsync(System.Net.Http.HttpResponseMessage response)
                {
                    var apiResponse = new FileApiResponse
                    {
                        Data = await response.Content.ReadAsStreamAsync().ConfigureAwait(false)
                    };

                    return apiResponse;
                }

                public Stream Data { get; private set; }
            }
            """;

        var apiResponseDecl = ParseTextToClassDeclaration(apiResponseClassContent);
        var apiResponseTDecl = ParseTextToClassDeclaration(apiResponseTClassContent);
        var fileApiResponseDecl = ParseTextToClassDeclaration(fileApiResponseClassContent);

        var nsDecl = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(namespaceName))
            .AddMembers(apiResponseDecl, apiResponseTDecl, fileApiResponseDecl);

        var formattedCode = nsDecl.NormalizeWhitespace().ToFullString();

        context.AddSource($"ApiResponse.g.cs", formattedCode);
    }

    private static ClassDeclarationSyntax ParseTextToClassDeclaration(string classContent) 
        => CSharpSyntaxTree.ParseText(classContent).GetCompilationUnitRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
    private static EnumDeclarationSyntax ParseTextToEnumDefinition(string enumContent)
       => CSharpSyntaxTree.ParseText(enumContent).GetCompilationUnitRoot().DescendantNodes().OfType<EnumDeclarationSyntax>().Single();

    private void GenerateOperationMethod(KeyValuePair<string, OpenApiPathItem> path, KeyValuePair<OperationType, OpenApiOperation> operation)
    {
        var methodName = GetMethodName(path, operation);


        foreach (var response in operation.Value.Responses)
        {
            foreach(var content in response.Value.Content)
            {
                if (content.Key == "application/json")
                {
                    var schema = content.Value.Schema;
                    var typeName = GetOrGenerateModelFromSchema(schema, schema.Reference?.Id ?? path.Key.Replace("/", "_"));
                }
            }
        }
        

        var methodContent = $$"""
            /// <summary> {{operation.Value.Summary}} <br /> {{operation.Value.Description}} </summary>
            /// <remarks> {{operation.Key.ToHttpMethod()}} {{path.Key}} </remarks>
            public async Task<ApiResponse> {{methodName}}Async()
            {
                await Task.Delay(0);
                throw new NotImplementedException("{{methodName}} is not implemented yet.");
            }
            """;
        var method = new OpenAPIDocumentOperationMethodDefinition
        {
            PathName = path.Key,
            OperationName = operation.Key,
            MethodName = methodName,
            MethodContent = methodContent
        };
        operations.Add(method);
    }


    private readonly Dictionary<string, string> _generatedModels = new();
    private readonly Dictionary<string, string> _generatedEnums = new();

    private string GetOrGenerateModelFromSchema(OpenApiSchema schema, string modelName)
    {
        if(_generatedModels.ContainsKey(modelName.ToPascalCase()))
        {
            return modelName.ToPascalCase(); // Already generated
        }
        if(_generatedEnums.ContainsKey(modelName.ToPascalCase()))
        {
            return modelName.ToPascalCase(); // Already generated
        }


        // Handle arrays
        if (schema.Type == "array" && schema.Items != null)
        {
            var itemType = GetOrGenerateModelFromSchema(schema.Items, schema.Items.Reference?.Id ?? modelName);
            return $"List<{itemType}>";
        }

        // Handle primitives
        if (schema.Type != null && schema.Type != "object")
        {
            // Handle enums
            if (schema.Enum?.Any() ?? false)
            {
                return CreateEnum(schema, modelName);
            }
            return MapPrimitive(schema.Type, schema.Format);
        }
        
        // Handle dictionary-like objects
        if (schema.Type == "object" && schema.Properties.Count == 0 && schema.AdditionalProperties != null)
        {
            var valueType = GetOrGenerateModelFromSchema(schema.AdditionalProperties, modelName);
            return $"Dictionary<string, {valueType}>";
        }

        // Handle objects
        if (schema.Type == "object" || schema.Properties.Count > 0)
        {
            return CreateModelClass(schema, modelName);
        }

        return "object";
    }

    private string CreateModelClass(OpenApiSchema objectSchema, string modelName)
    {
        var className = modelName.ToPascalCase();

        if (!_generatedModels.ContainsKey(className))
        {
            var props = new List<string>();

            foreach (var prop in objectSchema.Properties)
            {
                var propType = GetOrGenerateModelFromSchema(prop.Value, prop.Key);
                props.Add($"public {propType} {prop.Key.ToPascalCase()} {{ get; set; }}");
            }

            var properties = props.Select(x => SyntaxFactory.ParseMemberDeclaration(x)!).ToArray();

            var classDecl = SyntaxFactory.ClassDeclaration(className)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.PartialKeyword))
                .AddMembers(properties);

            _generatedModels[className] = classDecl.NormalizeWhitespace().ToFullString();
        }
        return className;
    }

    private string CreateEnum(OpenApiSchema enumSchema, string modelName)
    {
        var className = modelName.ToPascalCase();
        if (!_generatedEnums.ContainsKey(className))
        {
            var enumValues = enumSchema.Enum.OfType<OpenApiString>().Select(x => x.Value.ToString()).ToArray();
            var enumDecl = SyntaxFactory.EnumDeclaration(className)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddMembers(enumValues.Select(v => SyntaxFactory.EnumMemberDeclaration(v.ToPascalCase())).ToArray());
            _generatedEnums[className] = enumDecl.NormalizeWhitespace().ToFullString();
        }
        return className;
    }

    private string MapPrimitive(string type, string format)
    {
        return type switch
        {
            "integer" => MapIntegerFormat(format),
            "number" => MapNumberFormat(format),
            "boolean" => "bool",
            "string" => MapNumberFormat(format),
            _ => "object"
        };
    }
    private string MapIntegerFormat(string format)
    {
        return format switch
        {
            "int32" => "int",
            "int64" => "long",
            _ => "int"
        };
    }

    private string MapNumberFormat(string format)
    {
        return format switch
        {
            "float" => "float",
            "double" => "double",
            "decimal" => "decimal",
            _ => "double"
        };
    }

    private string MapStringFormat(string format)
    {
        return format switch
        {
            "date-time" => "DateTime",
            "date" => "DateOnly",
            "uuid" => "Guid",
            _ => "string"
        };
    }

    private OpenApiSchema ResolveSchema(string refId)
    {
        // Lookup from your OpenApiDocument.Components.Schemas
        return data.OpenAPIDocument.Components.Schemas[refId];
    }

    private static string GetMethodName(KeyValuePair<string, OpenApiPathItem> path, KeyValuePair<OperationType, OpenApiOperation> operation)
    {
        if (!string.IsNullOrWhiteSpace(operation.Value.OperationId))
            return operation.Value.OperationId.UpperCaseFirstChar();

        return "";
    }
}