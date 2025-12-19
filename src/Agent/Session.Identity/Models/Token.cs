using System.IdentityModel.Tokens.Jwt;

namespace Session.Identity.Models;

public class Token
{
    public required JwtSecurityToken JwtToken { get; set; }
    public required string Raw { get; set; }
}
