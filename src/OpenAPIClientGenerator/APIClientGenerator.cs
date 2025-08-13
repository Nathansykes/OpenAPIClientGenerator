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
            return new
            {
                Path = file.Path,
                Content = text
            };
        });

        // Step 3: Gather all annotated classes
        var annotatedClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax cds && cds.AttributeLists.Count > 0,
                transform: static (ctx, _) =>
                {
                    var symbol = ctx.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)ctx.Node);
                    if (symbol is null)
                        return null;
                    var attr = symbol.GetAttributes()
                        .FirstOrDefault(a => a.AttributeClass?.Name == nameof(APIClientAttribute));
                    if (attr == null)
                        return null;

                    var fileName = attr.ConstructorArguments[0].Value as string ?? "";
                    return new
                    {
                        Symbol = symbol,
                        TargetFileName = fileName
                    };
                })
            .Where(x => x != null);

        // Step 4: Join the attribute info with the additional file content
        var joined = annotatedClasses
            .Combine(openApiFiles.Collect()) // collect into list so we can search
            .Select((data, _) =>
            {
                var (classInfo, files) = data;
                if (classInfo is null || files.Length == 0)
                    return null;

                var match = files.FirstOrDefault(f => f.Path.EndsWith(classInfo.TargetFileName, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                    return null;

                var reader = new OpenApiStringReader();
                var apiDocument = reader.Read(match.Content, out OpenApiDiagnostic? diag);

                return new APIDocumentSourceData(classInfo.Symbol, apiDocument);
            })
            .Where(x => x != null);


        // Step 5: Generate the client
        context.RegisterSourceOutput(joined, (sourceProductionContext, data) =>
        {
            var handler = new APIDocumentHandler(sourceProductionContext, data!);
            handler.GenerateCode();

        });
    }
}
