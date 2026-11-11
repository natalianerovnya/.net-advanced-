using System.Text.Json.Serialization;
using JobScheduler.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// MVC and the OpenAPI generator read different JSON options; configure both so enums are names everywhere.
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "JobScheduler API";
        document.Info.Version = "v1";
        return Task.CompletedTask;
    });
});

// Connection strings come from user-secrets locally and Key Vault / app settings in Azure; see README.
builder.Services.AddJobSchedulerDataAccess(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "JobScheduler API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public partial class Program;
