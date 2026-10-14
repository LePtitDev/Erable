var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/api/info", () => "Erable");

await app.RunAsync().ConfigureAwait(false);
