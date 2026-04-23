using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IPlayerService _playerService;

    public AuthController(IPlayerService playerService) => _playerService = playerService;

    [HttpPost("login/{username}")]
    public async Task<IActionResult> Login(string username)
    {
        var player = _playerService.CreatePlayer(username);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, player.UserName!),
            new Claim("PlayerId", player.ID.ToString())
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity));
        return Ok(new { message = "Logged in successfully", playerId = player.ID });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var playerId = User.FindFirst("PlayerId")?.Value;
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { 
            message = "Logged out successfully", 
            loggedOutPlayerId = playerId 
        });
    }
}