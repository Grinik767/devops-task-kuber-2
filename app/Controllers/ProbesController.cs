using HomeworkApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomeworkApp.Controllers;

[ApiController]
[Produces("application/json")]
[Route("/")]
public class ProbesController(ProbeState state, ILogger<ProbesController> logger) : ControllerBase
{
    [HttpGet("live")]
    public IActionResult Live() => state.IsLive ? Ok("live") : StatusCode(503);

    [HttpGet("ready")]
    public IActionResult Ready() => state.IsReady ? Ok("ready") : StatusCode(503);

    [HttpGet("you-are-death")]
    public IActionResult FailLiveness()
    {
        state.FailLiveness();
        logger.LogWarning("Liveness failure enabled");
        return Ok("liveness disabled until process restart");
    }

    [HttpGet("not-ready")]
    public IActionResult DisableReadiness()
    {
        state.DisableReadiness();
        logger.LogWarning("Readiness disabled");
        return Ok("readiness disabled");
    }

    [HttpGet("ready-again")]
    public IActionResult RestoreReadiness()
    {
        state.RestoreReadiness();
        logger.LogInformation("Readiness restored");
        return Ok("readiness restored; initial startup delay still applies");
    }
}
