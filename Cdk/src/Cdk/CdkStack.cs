using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.SSM;
using Constructs;
using System;
using System.Collections.Generic;
using StageOptions = Amazon.CDK.AWS.APIGateway.StageOptions;

namespace Cdk {
    public class CdkStack : Stack {
        internal CdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props) {
            string appName = System.Environment.GetEnvironmentVariable("APP_NAME") ?? throw new ArgumentNullException("APP_NAME");
            string publishZip = System.Environment.GetEnvironmentVariable("PUBLISH_ZIP") ?? throw new ArgumentNullException("PUBLISH_ZIP");
            string handler = System.Environment.GetEnvironmentVariable("HANDLER") ?? throw new ArgumentNullException("HANDLER");
            string timeout = System.Environment.GetEnvironmentVariable("TIMEOUT") ?? throw new ArgumentNullException("TIMEOUT");
            string memorySize = System.Environment.GetEnvironmentVariable("MEMORY_SIZE") ?? throw new ArgumentNullException("MEMORY_SIZE");
            string domainName = System.Environment.GetEnvironmentVariable("DOMAIN_NAME") ?? throw new ArgumentNullException("DOMAIN_NAME");
            string apiMappingKey = System.Environment.GetEnvironmentVariable("API_MAPPING_KEY") ?? throw new ArgumentNullException("API_MAPPING_KEY");
            string vpcId = System.Environment.GetEnvironmentVariable("VPC_ID") ?? throw new ArgumentNullException("VPC_ID");
            string subnetId1 = System.Environment.GetEnvironmentVariable("SUBNET_ID_1") ?? throw new ArgumentNullException("SUBNET_ID_1");
            string subnetId2 = System.Environment.GetEnvironmentVariable("SUBNET_ID_2") ?? throw new ArgumentNullException("SUBNET_ID_2");
            string allowedDomains = System.Environment.GetEnvironmentVariable("ALLOWED_DOMAINS") ?? throw new ArgumentNullException("ALLOWED_DOMAINS");

            // Variables de entorno de la lambda...
            string secretArnConnectionString = System.Environment.GetEnvironmentVariable("SECRET_ARN_CONNECTION_STRING") ?? throw new ArgumentNullException("SECRET_ARN_CONNECTION_STRING");
            string parameterArnCognitoRegion = System.Environment.GetEnvironmentVariable("PARAMETER_ARN_COGNITO_REGION") ?? throw new ArgumentNullException("PARAMETER_ARN_COGNITO_REGION");
            string parameterArnCognitoUserPoolId = System.Environment.GetEnvironmentVariable("PARAMETER_ARN_COGNITO_USER_POOL_ID") ?? throw new ArgumentNullException("PARAMETER_ARN_COGNITO_USER_POOL_ID");
            string parameterArnCognitoUserPoolClientId = System.Environment.GetEnvironmentVariable("PARAMETER_ARN_COGNITO_USER_POOL_CLIENT_ID") ?? throw new ArgumentNullException("PARAMETER_ARN_COGNITO_USER_POOL_CLIENT_ID");
            string parameterNameApiAllowedDomains = System.Environment.GetEnvironmentVariable("PARAMETER_NAME_API_ALLOWED_DOMAINS") ?? throw new ArgumentNullException("PARAMETER_NAME_API_ALLOWED_DOMAINS");

            // Se obtiene la VPC y subnets...
            IVpc vpc = Vpc.FromLookup(this, $"{appName}Vpc", new VpcLookupOptions {
                VpcId = vpcId
            });

            ISubnet subnet1 = Subnet.FromSubnetId(this, $"{appName}Subnet1", subnetId1);
            ISubnet subnet2 = Subnet.FromSubnetId(this, $"{appName}Subnet2", subnetId2);

            // Se crea security group para la lambda...
            SecurityGroup securityGroup = new(this, $"{appName}LambdaSecurityGroup", new SecurityGroupProps {
                Vpc = vpc,
                SecurityGroupName = $"{appName}LambdaSecurityGroup",
                Description = $"{appName} Lambda Security Group",
                AllowAllOutbound = true,
            });

            // Creación de log group lambda...
            LogGroup logGroup = new(this, $"{appName}LogGroup", new LogGroupProps {
                LogGroupName = $"/aws/lambda/{appName}LambdaFunction/logs",
                Retention = RetentionDays.ONE_MONTH,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            // Creación de parametro en parameter store...
            StringParameter stringParameterApiAllowedDomains = new(this, $"{appName}StringParameterAllowedDomains", new StringParameterProps {
                ParameterName = $"{parameterNameApiAllowedDomains}",
                Description = $"Allowed Domains de la aplicacion {appName}",
                StringValue = allowedDomains,
                Tier = ParameterTier.STANDARD,
            });

            // Creación de role para la función lambda...
            IRole roleLambda = new Role(this, $"{appName}LambdaRole", new RoleProps {
                RoleName = $"{appName}LambdaRole",
                Description = $"Role para API Lambda de {appName}",
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
                ManagedPolicies = [
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaVPCAccessExecutionRole"),
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole"),
                ],
                InlinePolicies = new Dictionary<string, PolicyDocument> {
                    {
                        $"{appName}LambdaPolicy",
                        new PolicyDocument(new PolicyDocumentProps {
                            Statements = [
                                new PolicyStatement(new PolicyStatementProps{
                                    Sid = $"{appName}AccessToParameterStore",
                                    Actions = [
                                        "ssm:GetParameter"
                                    ],
                                    Resources = [
                                        stringParameterApiAllowedDomains.ParameterArn,
                                    ],
                                })
                            ]
                        })
                    }
                }
            });

            // Creación de la función lambda...
            Function function = new(this, $"{appName}LambdaFunction", new FunctionProps {
                Runtime = Runtime.DOTNET_8,
                Handler = handler,
                Code = Code.FromAsset(publishZip),
                FunctionName = $"{appName}LambdaFunction",
                Timeout = Duration.Seconds(double.Parse(timeout)),
                MemorySize = double.Parse(memorySize),
                LogGroup = logGroup,
                Environment = new Dictionary<string, string> {
                    { "APP_NAME", appName },
                    { "SECRET_ARN_CONNECTION_STRING", secretArnConnectionString },
                    { "PARAMETER_ARN_COGNITO_REGION", parameterArnCognitoRegion },
                    { "PARAMETER_ARN_COGNITO_USER_POOL_ID", parameterArnCognitoUserPoolId },
                    { "PARAMETER_ARN_COGNITO_USER_POOL_CLIENT_ID", parameterArnCognitoUserPoolClientId },
                    { "PARAMETER_ARN_API_ALLOWED_DOMAINS", stringParameterApiAllowedDomains.ParameterArn },
                },
                Vpc = vpc,
                VpcSubnets = new SubnetSelection {
                    Subnets = [subnet1, subnet2]
                },
                SecurityGroups = [securityGroup],
                Role = roleLambda,
            });

            // Creación de access logs...
            LogGroup logGroupAccessLogs = new(this, $"{appName}LambdaFunctionLogGroup", new LogGroupProps {
                LogGroupName = $"/aws/lambda/{appName}LambdaFunction/access_logs",
                Retention = RetentionDays.ONE_MONTH,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            // Se crea authorizer para el apigateway...
            IStringParameter stringParameterCognitoUserPoolId = StringParameter.FromStringParameterArn(this, $"{appName}StringParameterCognitoUserPoolId", parameterArnCognitoUserPoolId);
            IUserPool userPool = UserPool.FromUserPoolId(this, $"{appName}APIUserPool", stringParameterCognitoUserPoolId.StringValue);
            CognitoUserPoolsAuthorizer cognitoUserPoolsAuthorizer = new(this, $"{appName}Authorizer", new CognitoUserPoolsAuthorizerProps {
                CognitoUserPools = [userPool],
                AuthorizerName = $"{appName}Authorizer",
            });

            // Creación de la LambdaRestApi...
            LambdaRestApi lambdaRestApi = new(this, $"{appName}LambdaRestApi", new LambdaRestApiProps {
                Handler = function,
                DefaultCorsPreflightOptions = new CorsOptions {
                    AllowOrigins = stringParameterApiAllowedDomains.StringValue.Split(","),
                },
                DeployOptions = new StageOptions {
                    AccessLogDestination = new LogGroupLogDestination(logGroupAccessLogs),
                    AccessLogFormat = AccessLogFormat.Custom("'{\"requestTime\":\"$context.requestTime\",\"requestId\":\"$context.requestId\",\"httpMethod\":\"$context.httpMethod\",\"path\":\"$context.path\",\"resourcePath\":\"$context.resourcePath\",\"status\":$context.status,\"responseLatency\":$context.responseLatency,\"xrayTraceId\":\"$context.xrayTraceId\",\"integrationRequestId\":\"$context.integration.requestId\",\"functionResponseStatus\":\"$context.integration.status\",\"integrationLatency\":\"$context.integration.latency\",\"integrationServiceStatus\":\"$context.integration.integrationStatus\",\"authorizeStatus\":\"$context.authorize.status\",\"authorizerStatus\":\"$context.authorizer.status\",\"authorizerLatency\":\"$context.authorizer.latency\",\"authorizerRequestId\":\"$context.authorizer.requestId\",\"ip\":\"$context.identity.sourceIp\",\"userAgent\":\"$context.identity.userAgent\",\"principalId\":\"$context.authorizer.principalId\"}'"),
                    MetricsEnabled = true,
                    StageName = "prod",
                    Description = $"Stage para produccion de la aplicacion {appName}",
                },
                RestApiName = $"{appName}LambdaRestApi",
                DefaultMethodOptions = new MethodOptions {
                    AuthorizationType = AuthorizationType.COGNITO,
                    Authorizer = cognitoUserPoolsAuthorizer
                },
            });

            // Creación de la CfnApiMapping para el API Gateway...
            CfnApiMapping apiMapping = new(this, $"{appName}ApiMapping", new CfnApiMappingProps {
                DomainName = domainName,
                ApiMappingKey = apiMappingKey,
                ApiId = lambdaRestApi.RestApiId,
                Stage = lambdaRestApi.DeploymentStage.StageName,
            });

            // Se configura permisos para la ejecucíon de la Lambda desde el API Gateway...
            ArnPrincipal arnPrincipal = new("apigateway.amazonaws.com");
            Permission permission = new() {
                Scope = this,
                Action = "lambda:InvokeFunction",
                Principal = arnPrincipal,
                SourceArn = $"arn:aws:execute-api:{this.Region}:{this.Account}:{lambdaRestApi.RestApiId}/*/*/*",
            };
            function.AddPermission($"{appName}Permission", permission);
        }
    }
}
