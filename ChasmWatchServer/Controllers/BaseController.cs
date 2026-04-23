using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

[Authorize]
[ApiController]
public abstract class BaseController : ControllerBase
{
    protected Guid CurrentPlayerId
    {
        get
        {
            var idValue = User.FindFirst("PlayerId")?.Value;
            return Guid.TryParse(idValue, out var guid) ? guid : Guid.Empty;
        }
    }
    protected string CurrentPlayerName => User.Identity?.Name ?? "Unknown";
}