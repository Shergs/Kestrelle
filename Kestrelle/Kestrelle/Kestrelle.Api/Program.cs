using Kestrelle.Api.Status;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

var api = app.MapGroup("/api");

GetStatus.Map(api);

app.Run();