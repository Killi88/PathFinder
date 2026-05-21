using System;
using System.IO;
using System.Linq;
using PathFinder.Services;
using Newtonsoft.Json.Linq;

var urls = new[] {
    "https://api.swaggerhub.com/apis/dcsaorg/DCSA_EBL",
    "https://api.swaggerhub.com/apis/dcsaorg/DCSA_BKG"
};
var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestFiles", "TransportDocumentNotification.json"));
var result = await DcsaService.SortJsonBySchemaAsync(json, urls);
Console.WriteLine("Schema: " + result.SchemaName);
Console.WriteLine("MatchPath: " + result.ApiName);

var sorted = JObject.Parse(result.SortedJson);
var data = sorted["JsonContent"]?["data"] as JObject;
if (data != null) {
    Console.WriteLine("=== data property order ===");
    foreach (var p in data.Properties())
        Console.WriteLine("  " + p.Name);
}
