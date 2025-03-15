using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;

namespace MateoApiAotCdk {

    [JsonSerializable(typeof(APIGatewayProxyRequest))]
    [JsonSerializable(typeof(APIGatewayProxyResponse))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    public partial class CustomSerializationContext: JsonSerializerContext {
    }
}
