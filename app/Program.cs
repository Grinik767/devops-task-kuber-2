using System.Text.Encodings.Web;
using System.Text.Unicode;
using HomeworkApp.Controllers;
using HomeworkApp.Services;

var builder = WebApplication.CreateBuilder(args);

var listenPort = builder.Configuration["APP_PORT"] ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{listenPort}");

builder.Services
    .AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All));

builder.Services.AddSingleton(new ProbeState());

var app = builder.Build();

var stage = app.Configuration["APP_ENVIRONMENT"] ?? InfoController.FallbackStage;
var hasToken = !string.IsNullOrEmpty(app.Configuration["APP_TOKEN"]);

app.Logger.LogInformation(
    "Starting {Version} on port {Port}: environment={Environment}, tokenConfigured={TokenConfigured}",
    InfoController.BuildVersion, listenPort, stage, hasToken);

app.MapControllers();

app.Run();
