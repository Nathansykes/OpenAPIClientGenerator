using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using OpenAPIClientGenerator.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenAPIClientGenerator;

[Generator(LanguageNames.CSharp)]
internal class APIClientGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Step 1: Gather all additional files
        var additionalFiles = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

        // Step 2: Map to file content
        var openApiFiles = additionalFiles.Select((file, cancellationToken) =>
        {
            var text = file.GetText(cancellationToken)?.ToString() ?? "";
            return (Path: file.Path, Content: text);
        });

        // Step 3: Gather all annotated classes
        var annotatedClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax cds && cds.AttributeLists.Count > 0,
                transform: static (ctx, _) =>
                {
                    var symbol = ctx.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)ctx.Node);
                    var attr = symbol?.GetAttributes()
                        .FirstOrDefault(a => a.AttributeClass?.Name == nameof(APIClientAttribute));
                    if (attr == null)
                        return null;

                    var fileName = attr.ConstructorArguments[0].Value as string ?? "";
                    return ((ISymbol Symbol,string FileName)?)(Symbol: symbol!, FileName: fileName);
                })
            .Where(x => x != null)!;

        // Step 4: Join the attribute info with the additional file content
        var joined = annotatedClasses
            .Combine(openApiFiles.Collect()) // collect into list so we can search
            .Select((data, _) =>
            {
                var (classInfo, files) = data;
                var match = files.FirstOrDefault(f => f.Path.EndsWith(classInfo?.FileName, StringComparison.OrdinalIgnoreCase));

                var reader = new OpenApiStringReader();
                var apiDocument = reader.Read(match.Content, out OpenApiDiagnostic? diag);

                return (classInfo?.Symbol, SpecContent: match.Content, ApiDocument: apiDocument);
            });

        // Step 5: Generate the client
        context.RegisterSourceOutput(joined, (spc, data) =>
        {
            if (data.Symbol is null)
                return;
            var namespaceName = data.Symbol.ContainingNamespace.ToDisplayString();
            var className = data.Symbol.Name;
            if (className is null)
                return;

            HashSet<OpenAPIDocumentModelClassDefinition> models = [];
            HashSet<OpenAPIDocumentOperationMethodDefinition> operations = [];
            foreach (KeyValuePair<string, OpenApiPathItem> path in data.ApiDocument.Paths)
            {
                foreach (KeyValuePair<OperationType, OpenApiOperation> operation in path.Value.Operations)
                {
                    var methodName = GetMethodName(path, operation);
                    var methodContent = $$"""
                    /// <summary> {{operation.Value.Summary}} <br /> {{operation.Value.Description}} </summary>
                    /// <remarks> {{operation.Key.ToString().ToUpper()}} {{path.Key}} </remarks>
                    public async Task {{methodName}}Async()
                    {
                        throw new NotImplementedException("{{methodName}} is not implemented yet.");
                    }
                    """;
                    operations.Add(new OpenAPIDocumentOperationMethodDefinition
                    {
                        PathName = path.Key,
                        OperationName = operation.Key,
                        MethodName = methodName,
                        MethodContent = methodContent
                    });
                }
            }

            var methods = operations.Select(op => SyntaxFactory.ParseMemberDeclaration(op.MethodContent)!);

            var classDecl = SyntaxFactory.ClassDeclaration(className)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.PartialKeyword));

            foreach (var method in methods)
            {
                classDecl = classDecl.AddMembers(method);
            }

            var nsDecl = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(namespaceName))
                .AddMembers(classDecl);

            var formattedCode = nsDecl.NormalizeWhitespace().ToFullString();

            spc.AddSource($"{className}.g.cs", formattedCode);
        });
    }



    private static string GetMethodName(KeyValuePair<string, OpenApiPathItem> path, KeyValuePair<OperationType, OpenApiOperation> operation)
    {
        if (!string.IsNullOrWhiteSpace(operation.Value.OperationId))
            return operation.Value.OperationId.UpperCaseFirstChar();

        return "";
    }
    private class OpenAPIDocumentModelClassDefinition
    {
        public string SchemaName { get; set; } = null!;
        public string ClassName { get; set; } = null!;
        public string ClassContent { get; set; } = null!;

        public override int GetHashCode()
        {
            return ClassContent.GetHashCode();
        }
    }
    private class OpenAPIDocumentOperationMethodDefinition
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
}

file static class Extensions
{
    public static string UpperCaseFirstChar(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        return char.ToUpper(value[0]) + value.Substring(1);
    }
}

