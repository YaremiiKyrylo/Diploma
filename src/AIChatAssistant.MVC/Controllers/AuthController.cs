using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using AIChatAssistant.MVC.Models.DTO;
using AIChatAssistant.Domain.Entities;

namespace AIChatAssistant.MVC.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _userManager = userManager;
        _configuration = configuration;
        _environment = environment;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        var claims = new List<Claim>
        {
            new Claim("Company", request.Company),
            new Claim("JobTitle", request.JobTitle)
        };

        await _userManager.AddClaimsAsync(user, claims);
        await _userManager.AddToRoleAsync(user, "User");

        return Ok(new { message = "User registered successfully." });
    }



    /// <summary>
    /// Assign Admin role to a user (Development helper). Requires existing Admin JWT or run from SQL.
    /// </summary>
    [HttpPost("assign-admin")]
    public async Task<IActionResult> AssignAdmin([FromBody] AssignAdminRequest request)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null) return NotFound(new { error = "User not found" });

        if (!await _userManager.IsInRoleAsync(user, "Admin"))
            await _userManager.AddToRoleAsync(user, "Admin");

        return Ok(new { message = $"User {request.Email} is now Admin." });
    }

    public class AssignAdminRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized("Invalid email or password.");

        // 3. Collecting Claims for the JWT token
        var authClaims = new List<Claim>
        {
        // Standard claims (ID and Email)
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), // Our int Id
        new Claim(ClaimTypes.Name, user.Email),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        // Retrieve custom claims from the database and add them to the token
        var userClaims = await _userManager.GetClaimsAsync(user);
        authClaims.AddRange(userClaims);

        // Get roles (if any) and add them to the token
        var userRoles = await _userManager.GetRolesAsync(user);
        foreach (var role in userRoles)
        {
            authClaims.Add(new Claim(ClaimTypes.Role, role));
        }

        // 4. Generate the JWT token itself
        var token = GenerateJwtToken(authClaims);

        return Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token),
            expiration = token.ValidTo
        });
    }

    private JwtSecurityToken GenerateJwtToken(List<Claim> authClaims)
    {
        var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:Secret"]));
        var expirationDays = Convert.ToInt32(_configuration["JwtSettings:ExpirationInDays"]);

        var token = new JwtSecurityToken(
            issuer: _configuration["JwtSettings:Issuer"],
            audience: _configuration["JwtSettings:Audience"],
            expires: DateTime.UtcNow.AddDays(expirationDays),
            claims: authClaims,
            signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
        );

        return token;
    }
}