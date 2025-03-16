﻿using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Newtonsoft.Json;

namespace MateoApiAotCdk.Helpers.AwsServices {
    public class SecretManager {
        public static async Task<Dictionary<string, string>> ObtenerSecreto(string secretArn) {
            IAmazonSecretsManager client = new AmazonSecretsManagerClient();
            GetSecretValueResponse response = await client.GetSecretValueAsync(new GetSecretValueRequest {
                SecretId = secretArn
            });

            if (response == null || response.SecretString == null) {
                throw new Exception("No se pudo rescatar correctamente el secreto");
            }

            return JsonConvert.DeserializeObject<Dictionary<string, string>>(response.SecretString)!;
        }
    }
}
