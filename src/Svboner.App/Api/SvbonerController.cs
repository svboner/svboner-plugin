using Microsoft.AspNetCore.Mvc;
using Svboner.Core.Config;
using Svboner.Core.Models;
using Svboner.Core.Services;

namespace Svboner.App.Api;

/// <summary>REST endpoints consumed by the web UI.</summary>
[ApiController]
[Route("api")]
public sealed class SvbonerController : ControllerBase
{
    private readonly SvbonerOrchestrator _orchestrator;
    private readonly ConfigStore _cfg;

    public SvbonerController(SvbonerOrchestrator orchestrator, ConfigStore cfg)
    {
        _orchestrator = orchestrator;
        _cfg          = cfg;
    }

    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(_orchestrator.GetStatus());

    [HttpGet("config")]
    public IActionResult GetConfig() => Ok(_cfg.Get());

    [HttpPost("config")]
    public IActionResult SaveConfig([FromBody] SvbonerConfig config)
    {
        _cfg.Replace(config);
        _orchestrator.ReloadConfig();
        return Ok(_cfg.Get());
    }

    [HttpPost("output/enable")]
    public IActionResult EnableOutput()
    {
        _cfg.Update(c => c.Global.OutputEnabled = true);
        return Ok();
    }

    [HttpPost("output/disable")]
    public IActionResult DisableOutput()
    {
        _cfg.Update(c => c.Global.OutputEnabled = false);
        return Ok();
    }

    [HttpPost("panic")]
    public async Task<IActionResult> Panic()
    {
        await _orchestrator.PanicStopAsync();
        return Ok();
    }
}
