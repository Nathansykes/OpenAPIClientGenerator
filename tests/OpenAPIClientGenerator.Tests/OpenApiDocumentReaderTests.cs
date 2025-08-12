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
        var apiDocumentReader = new OpenAPIDocumentReader(content);

        //act
        var document = await apiDocumentReader.ReadDocumentAsync();

        //assert
        Assert.NotNull(document); //TODO: Add more assertions to validate the content of the document
    }
}
