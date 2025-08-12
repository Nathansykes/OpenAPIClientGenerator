using SampleConsoleApp;

internal class Program
{
    private static void Main(string[] args)
    {
        var client = new MyApiClient();
        Console.WriteLine(client.SpecPreview);
    }
}