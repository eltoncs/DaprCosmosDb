using Dapr.Client;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

public class DaprCosmosBindingFunction
{
    private readonly ILogger _logger;

    public DaprCosmosBindingFunction(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<DaprCosmosBindingFunction>();
    }

    [Function("DaprCosmosBindingFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var input = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);

        var data = new
        {
            id = Guid.NewGuid().ToString(),
            value = input["value"]
        };

        using var daprClient = new DaprClientBuilder().Build();
        await daprClient.InvokeBindingAsync(
            bindingName: "cosmosdb",
            operation: "create",
            data: data);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("Stored via Dapr CosmosDB binding (isolated function).\n");
        return response;
    }

    [Function("GetCosmosDataFunction")]
    public async Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "GetCosmosDataFunction/{id}")] HttpRequestData req,
        string id)
    {
        using var daprClient = new DaprClientBuilder().Build();
        var metadata = new Dictionary<string, string>
            {
                { "id", id },
                { "operation", "get" }
            };

        var responseData = await daprClient.InvokeBindingAsync<byte[]>(
            bindingName: "cosmosdb",
            operation: "get",
            data: new { },
            metadata: metadata);

        var response = req.CreateResponse(HttpStatusCode.OK);
        if (responseData != null && responseData.Length > 0)
        {
            var resultJson = System.Text.Encoding.UTF8.GetString(responseData);
            await response.WriteStringAsync(resultJson);
        }
        else
        {
            response.StatusCode = HttpStatusCode.NotFound;
            await response.WriteStringAsync($"Item with id {id} not found.");
        }

        return response;
    }
}