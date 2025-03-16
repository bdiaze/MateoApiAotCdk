using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;
using MateoApiAotCdk.Entities.Models;
using MateoApiAotCdk.Models;

namespace MateoApiAotCdk.Helpers.Serializers {

    [JsonSerializable(typeof(APIGatewayProxyRequest))]
    [JsonSerializable(typeof(APIGatewayProxyResponse))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(EntEntrenamiento))]
    [JsonSerializable(typeof(SalEntrenamiento))]
    [JsonSerializable(typeof(Entrenamiento))]
    [JsonSerializable(typeof(Todo[]))]
    public partial class CustomSerializationContext : JsonSerializerContext {
    }
}
