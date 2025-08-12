using OpenAPIClientGenerator.DocumentReaders;

namespace OpenAPIClientGenerator.Tests;

public class OpenApiDocumentReaderTests
{
    [Fact]
    public async Task ReadFromString()
    {
        //arrange
        var assembly = typeof(OpenApiDocumentReaderTests).Assembly;
        var resourceName = "OpenAPIClientGenerator.Tests.Resources.PetStore.yml";
        using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException("Missing Assembly Resource");
        using var resourceReader = new StreamReader(stream);
        var content = await resourceReader.ReadToEndAsync();
        var apiDocumentReader = new StringOpenAPIDocumentReader(content);

        //act
        var document = await apiDocumentReader.ReadDocumentAsync();

        //assert
        Assert.NotNull(document); //TODO: Add more assertions to validate the content of the document
    }

    [Fact]
    public async Task ReadFromHttp()
    {
        //arrange
        const string url = "https://raw.githubusercontent.com/swagger-api/swagger-petstore/refs/heads/master/src/main/resources/openapi.yaml"; //TODO: Use fixed release URL instead of branch
        var apiDocumentReader = new HttpOpenAPIDocumentReader(url);

        //act
        var document = await apiDocumentReader.ReadDocumentAsync();

        //assert
        Assert.NotNull(document); //TODO: Add more assertions to validate the content of the document
    }
}
