using D3B.Data.Interactives;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Reads the kinds of interactive elements, which the server names an element of the map by.</summary>
internal static class InteractivesExtractor
{
    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return [UnityDataTable.PathOf(client, "interactives")];
    }

    public static InteractiveCatalog Read(ClientInstall client)
    {
        try
        {
            var interactives = UnityDataTable.ReadById(client, "interactives").Select(record => new Interactive(record.Int32("id"), record.Int32("nameId")));
            var catalog = new InteractiveCatalog(interactives.OrderBy(interactive => interactive.Id).ToArray());

            Console.WriteLine($"Interactives: {catalog.Interactives.Length:N0}");

            return catalog;
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException($"The interactives' table does not fit what the extractor measured: {exception.Message}", exception);
        }
    }
}
