using SampleConsoleApp;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var client = new MyApiClient();
        await client.AddPetAsync();
    }
}