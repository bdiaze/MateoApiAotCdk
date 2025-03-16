using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using MateoApiAotCdk.Entities.Contexts;
using MateoApiAotCdk.Entities.Models;
using MateoApiAotCdk.Helpers.Serializers;
using MateoApiAotCdk.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.Security.Claims;
using MateoApiAotCdk.Helpers.AwsServices;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolver = CustomSerializationContext.Default;
});

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi, options => {
    options.Serializer = new SourceGeneratorLambdaJsonSerializer<CustomSerializationContext>();
});

#if RELEASE
string secretArnConnectionString = Environment.GetEnvironmentVariable("SECRET_ARN_CONNECTION_STRING") ?? throw new ArgumentNullException("SECRET_ARN_CONNECTION_STRING");
    string parameterArnCognitoRegion = Environment.GetEnvironmentVariable("PARAMETER_ARN_COGNITO_REGION") ?? throw new ArgumentNullException("PARAMETER_ARN_COGNITO_REGION");
    string parameterArnCognitoUserPoolId = Environment.GetEnvironmentVariable("PARAMETER_ARN_COGNITO_USER_POOL_ID") ?? throw new ArgumentNullException("PARAMETER_ARN_COGNITO_USER_POOL_ID");
    string parameterArnCognitoUserPoolClientId = Environment.GetEnvironmentVariable("PARAMETER_ARN_COGNITO_USER_POOL_CLIENT_ID") ?? throw new ArgumentNullException("PARAMETER_ARN_COGNITO_USER_POOL_CLIENT_ID");
    string parameterArnApiAllowedDomains = Environment.GetEnvironmentVariable("PARAMETER_ARN_API_ALLOWED_DOMAINS") ?? throw new ArgumentNullException("PARAMETER_ARN_API_ALLOWED_DOMAINS");

    Dictionary<string, string> connectionString = await SecretManager.ObtenerSecreto(secretArnConnectionString);
    string cognitoRegion = await ParameterStore.ObtenerParametro(parameterArnCognitoRegion);
    string cognitoUserPoolId = await ParameterStore.ObtenerParametro(parameterArnCognitoUserPoolId);
    string[] cognitoAppClientId = (await ParameterStore.ObtenerParametro(parameterArnCognitoUserPoolClientId)).Split(",");
    string[] allowedDomains = (await ParameterStore.ObtenerParametro(parameterArnApiAllowedDomains)).Split(",");
#else
    // Se crean variables vacias en formato DEBUG para habilitar las migraciones de EFCore...
    // Comando para migrar: dotnet ef migrations add [NombreMigración] --project MateoAPI
    Dictionary<string, string> connectionString = new() {
            { "Host", "" },
            { "Port", "" },
            { "MateoDatabase", "" },
            { "MateoAppUsername", "" },
            { "MateoAppPassword", "" }
        };
    string cognitoRegion = "";
    string cognitoUserPoolId = "";
    string[] cognitoAppClientId = [""];
    string[] allowedDomains = [""];
#endif

builder.Services.AddDbContextPool<MateoDbContext>(options => options.UseNpgsql(
    $"Server={connectionString["Host"]};Port={connectionString["Port"]};SslMode=prefer;" +
    $"Database={connectionString["MateoDatabase"]};User Id={connectionString["MateoDatabase"]};Password='{connectionString["MateoDatabase"]}';"
));

builder.Services.AddCors(item => {
    item.AddPolicy("CORSPolicy", builder => {
        builder.WithOrigins(allowedDomains)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddAuthorization();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options => {
        options.Authority = $"https://cognito-idp.{cognitoRegion}.amazonaws.com/{cognitoUserPoolId}";
        options.MetadataAddress = $"https://cognito-idp.{cognitoRegion}.amazonaws.com/{cognitoUserPoolId}/.well-known/openid-configuration";
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidAudiences = cognitoAppClientId,
            ValidateIssuerSigningKey = true,
            NameClaimType = ClaimTypes.NameIdentifier,
        };
    });

var app = builder.Build();

app.UseCors("CORSPolicy");

app.UseAuthentication();

app.UseAuthorization();

RouteGroupBuilder entrenamientoRouteGroup = app.MapGroup("/entrenamiento");
entrenamientoRouteGroup.MapGet("/listar", async (DateTime desde, DateTime hasta, MateoDbContext _context, ClaimsPrincipal user, int numPagina = 1, int cantElemPagina = 25) => {
    Stopwatch stopwatch = Stopwatch.StartNew();
    try {
        int cantTotalElementos = _context.Entrenamientos.Where(e => e.IdUsuario == user.Identity!.Name && e.Inicio >= desde && e.Inicio <= hasta).Count();
        int cantTotalPaginas = Convert.ToInt32(Math.Ceiling(Decimal.Divide(cantTotalElementos, cantElemPagina)));
        List<Entrenamiento> entrenamientos = await _context.Entrenamientos
            .AsNoTracking()
            .Where(e => e.IdUsuario == user.Identity!.Name && e.Inicio >= desde && e.Inicio <= hasta)
            .OrderBy(e => e.IdUsuario)
            .OrderByDescending(e => e.Inicio)
            .Skip((numPagina - 1) * cantElemPagina)
            .Take(cantElemPagina)
            .ToListAsync();
        LambdaLogger.Log(
        $"[GET] - [EntrenamientoController] - [Listar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
            $"Se obtienen correctamente los entrenamientos del usuario {user.Identity!.Name} para los filtros desde {desde:O}, hasta {hasta:O}, numPagina {numPagina} y cantElemPagina {cantElemPagina}: " +
            $"{entrenamientos.Count} de {cantTotalElementos} entrenamientos.");

        return Results.Ok(new SalEntrenamiento {
            Desde = desde,
            Hasta = hasta,
            Pagina = numPagina,
            TotalPaginas = cantTotalPaginas,
            CantidadElementosPorPagina = cantElemPagina,
            CantidadTotalEntrenamientos = cantTotalElementos,
            Entrenamientos = entrenamientos,
        });
    } catch (Exception ex) {
        LambdaLogger.Log(
        $"[GET] - [EntrenamientoController] - [Listar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
            $"Ocurrió un error al obtener los entrenamientos del usuario {user.Identity!.Name} para los filtros desde {desde:O}, hasta {hasta:O}, numPagina {numPagina} y cantElemPagina {cantElemPagina}. " +
            $"{ex}");
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
}).RequireAuthorization();


var sampleTodos = new Todo[] {
    new(1, "Walk the dog"),
    new(2, "Do the dishes", DateOnly.FromDateTime(DateTime.Now)),
    new(3, "Do the laundry", DateOnly.FromDateTime(DateTime.Now.AddDays(1))),
    new(4, "Clean the bathroom"),
    new(5, "Clean the car", DateOnly.FromDateTime(DateTime.Now.AddDays(2)))
};

var todosApi = app.MapGroup("/todos");
todosApi.MapGet("/", () => sampleTodos);
todosApi.MapGet("/{id}", (int id) =>
    sampleTodos.FirstOrDefault(a => a.Id == id) is { } todo
        ? Results.Ok(todo)
        : Results.NotFound());

app.Run();

public record Todo(int Id, string? Title, DateOnly? DueBy = null, bool IsComplete = false);
