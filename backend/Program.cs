var builder = WebApplication.CreateBuilder(args);
// Configure port with priority: appsettings -> PORT env var -> default 5117
var port = builder.Configuration.GetValue<string>("Server:Port") ??
           Environment.GetEnvironmentVariable("PORT") ??
           "5117";
// For Azure App Service, bind to all interfaces; for development, use localhost
var host = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production" ? "0.0.0.0" : "localhost";
builder.WebHost.UseUrls($"http://{host}:{port}");
// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyHeader()
               .AllowAnyMethod();
    });
});
// Add response compression for better performance
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.MimeTypes = new[]
    {
        "application/json",
        "text/json",
        "application/javascript",
        "text/css",
        "text/html"
    };
});
// Add memory cache for performance optimization
builder.Services.AddMemoryCache();

// Add HttpClient for Azure Cost Management API
builder.Services.AddHttpClient();

// Configure Azure settings
builder.Services.Configure<EnergyCalculator.Models.AzureConfiguration>(
    builder.Configuration.GetSection("Azure"));
// Register application services
builder.Services.AddScoped<EnergyCalculator.Services.IEnergyCalculatorService, EnergyCalculator.Services.EnergyCalculatorService>();
builder.Services.AddScoped<EnergyCalculator.Services.IEnergyStorageService, EnergyCalculator.Services.AzureStorageService>();
builder.Services.AddScoped<EnergyCalculator.Services.IAzureResourceService, EnergyCalculator.Services.AzureResourceService>();
builder.Services.AddScoped<EnergyCalculator.Services.IAzureResourceDiscoveryService, EnergyCalculator.Services.AzureResourceDiscoveryService>();
builder.Services.AddScoped<EnergyCalculator.Services.IAzureCostService, EnergyCalculator.Services.AzureCostService>();
// Register Azure services
builder.Services.AddSingleton(provider =>
{
    var connectionString = builder.Configuration.GetConnectionString("AzureStorage") ??
                          builder.Configuration.GetValue<string>("Azure:StorageConnectionString");
    return new Azure.Storage.Blobs.BlobServiceClient(connectionString);
});
builder.Services.AddSingleton(provider =>
{
    var connectionString = builder.Configuration.GetConnectionString("AzureStorage") ??
                          builder.Configuration.GetValue<string>("Azure:StorageConnectionString");
    return new Azure.Data.Tables.TableServiceClient(connectionString);
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Energy Calculator API",
        Version = "v1",
        Description = "API for calculating energy consumption of cloud resources"
    });
});
var app = builder.Build();
// Log the URL for developers
var environment = app.Environment.EnvironmentName;
Console.WriteLine($":rocket: Energy Calculator API starting in {environment} mode");
Console.WriteLine($":round_pushpin: Server URL: http://{host}:{port}");
if (environment == "Development")
{
    Console.WriteLine($":wrench: To use different port: dotnet run --Server:Port=5000");
    Console.WriteLine($":globe_with_meridians: Swagger UI: http://{host}:{port}/swagger");
}
// Configure the HTTP request pipeline.
// Enable Swagger in production for debugging
app.UseSwagger();
app.UseSwaggerUI();
// Enable response compression for better performance
app.UseResponseCompression();
// Only use HTTPS redirection in development
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors();
app.UseAuthorization();
// Add a simple test endpoint
app.MapGet("/", () => "Energy Calculator API is running!");
app.MapGet("/test", () => new { message = "Test endpoint working", timestamp = DateTime.UtcNow });
app.MapControllers();
app.Run();