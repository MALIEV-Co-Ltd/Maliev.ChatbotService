using Maliev.ChatbotService.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Maliev.ChatbotService.Api.Controllers;

/// <summary>
/// Health check endpoints for Kubernetes liveness and readiness probes.
/// </summary>
[ApiController]
[Route("chatbot")]
public class HealthController : ControllerBase
{
    private readonly ChatbotDbContext _dbContext;
    private readonly ILogger<HealthController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthController"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public HealthController(ChatbotDbContext dbContext, ILogger<HealthController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Liveness probe endpoint. Indicates if the application is running.
    /// </summary>
    /// <returns>A 200 OK response if the application is alive.</returns>
    [HttpGet("liveness")]
    public IActionResult GetLiveness()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Readiness probe endpoint. Indicates if the application is ready to receive traffic.
    /// </summary>
    /// <returns>A 200 OK response if the application is ready, 503 Service Unavailable otherwise.</returns>
    [HttpGet("readiness")]
    public async Task<IActionResult> GetReadiness()
    {
        try
        {
            // Check database connectivity
            await _dbContext.Database.CanConnectAsync();

            return Ok(new
            {
                status = "ready",
                timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness check failed");
            return StatusCode(503, new
            {
                status = "not ready",
                timestamp = DateTimeOffset.UtcNow,
                error = "Database connectivity check failed"
            });
        }
    }
}
