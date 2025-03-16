using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;
using MateoApiAotCdk.Models;

namespace MateoApiAotCdk.Helpers.Serializers {

    [JsonSerializable(typeof(APIGatewayProxyRequest))]
    [JsonSerializable(typeof(APIGatewayProxyResponse))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    [JsonSerializable(typeof(EntEntrenamiento))]
    [JsonSerializable(typeof(SalEntrenamiento))]
    public partial class CustomSerializationContext : JsonSerializerContext {
    }
}
