using Microsoft.AspNetCore.Mvc;

namespace HomeworkApp.Controllers;

[ApiController]
[Route("/")]
public class InfoController(IConfiguration config) : ControllerBase
{
    public const string StudentName = "Киселев Григорий Александрович";
    public static readonly string BuildVersion = System.IO.File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "version.txt")).Trim();
    public const string FallbackGreeting = "Сообщения нет";
    public const string FallbackStage = "Окружения нет";

    [HttpGet]
    public IActionResult Get()
    {
        var greeting = config["APP_MESSAGE"] ?? FallbackGreeting;
        var stage = config["APP_ENVIRONMENT"] ?? FallbackStage;
        var hasToken = !string.IsNullOrEmpty(config["APP_TOKEN"]);

        return Ok(new
        {
            message = greeting,
            author = StudentName,
            environment = stage,
            tokenConfigured = hasToken,
            machineName = config["POD_NAME"] ?? Environment.MachineName,
            appVersion = BuildVersion
        });
    }
}
