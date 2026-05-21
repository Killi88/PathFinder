using System.IO;
using PathFinder.Services;

namespace PathFinder.Tests;

public class DcsaServiceTests
{
    private static readonly List<string> DefaultUrls =
    [
        "https://api.swaggerhub.com/apis/dcsaorg/DCSA_EBL",
        "https://api.swaggerhub.com/apis/dcsaorg/DCSA_BKG"
    ];

    private static string TestFilePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", name);

    [Fact]
    [Trait("Category", "Network")]
    public async Task SortJsonBySchema_BookingRequest_IdentifiesCreateBooking()
    {
        var json = File.ReadAllText(TestFilePath("BookingRequest.json"));
        var result = await DcsaService.SortJsonBySchemaAsync(json, DefaultUrls);

        Assert.Equal("DCSA_BKG", result.ApiName);
        Assert.NotEmpty(result.ApiVersion);
        Assert.NotEmpty(result.SchemaName);
        Assert.Contains("Booking", result.SchemaName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Network")]
    public async Task SortJsonBySchema_BookingRequest_SortedJsonIsValidJson()
    {
        var json = File.ReadAllText(TestFilePath("BookingRequest.json"));
        var result = await DcsaService.SortJsonBySchemaAsync(json, DefaultUrls);

        // The sorted output must be parseable JSON
        var parsed = Newtonsoft.Json.Linq.JToken.Parse(result.SortedJson);
        Assert.NotNull(parsed);
    }

    [Fact]
    [Trait("Category", "Network")]
    public async Task SortJsonBySchema_BookingRequest_PreservesAllKeys()
    {
        var json = File.ReadAllText(TestFilePath("BookingRequest.json"));
        var original = Newtonsoft.Json.Linq.JObject.Parse(json);
        var result = await DcsaService.SortJsonBySchemaAsync(json, DefaultUrls);
        var sorted = Newtonsoft.Json.Linq.JObject.Parse(result.SortedJson);

        // All original top-level keys must still be present
        foreach (var prop in original.Properties())
            Assert.True(sorted.ContainsKey(prop.Name), $"Missing key: {prop.Name}");
    }

    [Fact]
    [Trait("Category", "Network")]
    public async Task SortJsonBySchema_TransportDocumentNotification_IdentifiesSchema()
    {
        var json = File.ReadAllText(TestFilePath("TransportDocumentNotification.json"));
        var result = await DcsaService.SortJsonBySchemaAsync(json, DefaultUrls);

        Assert.NotEmpty(result.SchemaName);
        Assert.NotEmpty(result.ApiVersion);
    }

    [Fact]
    [Trait("Category", "Network")]
    public async Task SortJsonBySchema_TransportDocumentNotification_SortedJsonIsValid()
    {
        var json = File.ReadAllText(TestFilePath("TransportDocumentNotification.json"));
        var result = await DcsaService.SortJsonBySchemaAsync(json, DefaultUrls);

        var parsed = Newtonsoft.Json.Linq.JToken.Parse(result.SortedJson);
        Assert.NotNull(parsed);
    }

    [Fact]
    [Trait("Category", "Network")]
    public async Task SortJsonBySchema_ShippingInstructionsNotification_IdentifiesSchema()
    {
        var json = File.ReadAllText(TestFilePath("ShippingInstructionsNotification.json"));
        var result = await DcsaService.SortJsonBySchemaAsync(json, DefaultUrls);

        Assert.NotEmpty(result.SchemaName);
        Assert.NotEmpty(result.ApiVersion);
    }

    [Fact]
    [Trait("Category", "Network")]
    public async Task SortJsonBySchema_InvalidJson_ThrowsException()
    {
        await Assert.ThrowsAnyAsync<Exception>(() =>
            DcsaService.SortJsonBySchemaAsync("{ invalid json }", DefaultUrls));
    }

    [Fact]
    [Trait("Category", "Network")]
    public async Task SortJsonBySchema_EmptyUrls_ThrowsException()
    {
        var json = File.ReadAllText(TestFilePath("BookingRequest.json"));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            DcsaService.SortJsonBySchemaAsync(json, Array.Empty<string>()));
    }

    [Fact]
    [Trait("Category", "Network")]
    public async Task SortJsonBySchema_TransportDocumentNotification_DataPropertyOrder()
    {
        var json = File.ReadAllText(TestFilePath("TransportDocumentNotification.json"));
        var result = await DcsaService.SortJsonBySchemaAsync(json, DefaultUrls);
        var sorted = Newtonsoft.Json.Linq.JObject.Parse(result.SortedJson);
        var data = sorted["JsonContent"]?["data"] as Newtonsoft.Json.Linq.JObject;

        Assert.NotNull(data);

        var props = data!.Properties().Select(p => p.Name).ToList();
        var statusIdx = props.IndexOf("transportDocumentStatus");
        var refIdx = props.IndexOf("transportDocumentReference");

        // Schema defines status before reference
        Assert.True(statusIdx < refIdx,
            $"transportDocumentStatus (index {statusIdx}) should come before transportDocumentReference (index {refIdx}). Actual order: {string.Join(", ", props)}");
    }
}
