var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();
builder.Services.AddSingleton<OrbitDatabase>();
if (Environment.GetEnvironmentVariable("PORT") is string port)
{
    builder.WebHost.UseUrls("http://0.0.0.0:" + port);
}

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 300_000);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddHttpClient("external", client => client.Timeout = TimeSpan.FromSeconds(45));
builder.Services.AddSingleton<WorkflowStore>();
builder.Services.AddSingleton<FeatureAccess>();
builder.Services.AddHostedService<WorkflowWorker>();
builder.Services.AddHostedService<ReminderWorker>();
builder.AddGoogleConnection();
var app = builder.Build();
var database = app.Services.GetRequiredService<OrbitDatabase>();
if (args.Contains("--migrate"))
{
    await database.Migrate();
    Console.WriteLine("Orbit migration applied.");
    return;
}

await database.Initialize();
app.Services.GetRequiredService<WorkflowStore>().Restore();
app.ConfigureDeployment(database);
app.UseAuthentication();
app.UseWorkspaceAccess(database);
app.MapWorkspaceEndpoints(database);
app.MapGoogleConnection();
app.MapDriveFolders();
app.MapAccountFeatures(database);
app.MapReminderEndpoints(database);
app.MapBillingEndpoints(database);
app.MapConnectionEndpoints(database);
app.MapWorkflowEndpoints();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");
app.Run();
