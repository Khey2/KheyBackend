using KheyBackend.DBContext;
using KheyBackend.DTOS;
using KheyBackend.Models;
using KheyBackend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
// Configuration env variables
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
// JWT imports 
using System.Text;
using BC = BCrypt.Net.BCrypt;

namespace KheyBackend.Controllers
{

    [ApiController]
    [Route("auth")]
    public class AuthController: ControllerBase
    {

        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        // Constructor
        public AuthController(AppDbContext context, IConfiguration configuration, IEmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> register(RegisterRequestDTO data)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Email == data.email);

            if (userExists)
            {
                return BadRequest(new { message = "El usuario no " });
            }


            string passwordHash = BC.HashPassword(data.password);


            var newUser = new User
            {
                Username = data.username,
                Email = data.email,
                PasswordHash = passwordHash , 
                IsEmailVerified = false ,
                CreatedAt = DateTime.UtcNow
            };


            _context.Add(newUser);
            await _context.SaveChangesAsync();

            // Generacion de JWT
            string jwt_token = GenerateJwtToken(
                user_id: newUser.Id ,
                username: newUser.Username ,
                expiration: TimeSpan.FromMinutes( 15 ), 
                purpose: "verify"
            );

            string emailHtmlBody = $"<div> <p> Hola { newUser.Username } este es el link de verificacion <span> {jwt_token} </span> </p> </div>";

            await _emailService.SendEmailAsync( newUser.Email, "Khey verificacion de cuenta" , emailHtmlBody  );

            return Ok(new
            {
                message = "Usuario registrado correctamente",
                jwt     = jwt_token
            });
        }


        // GENERACION DE TOKENS
        private string GenerateJwtToken( int user_id, string username , TimeSpan expiration , string purpose )
        {
            var claims = new[]
            {
                // ID del usuario como subject
                new Claim(JwtRegisteredClaimNames.Sub, user_id.ToString() ),
                // Username
                new Claim(  "username", username ) , 
                // Identificado unicod el token (JTI )
                new Claim(  JwtRegisteredClaimNames.Jti , Guid.NewGuid().ToString()  ),
                // Propósito explícito para evitar uso cruzado de tokens
                new Claim("purpose", purpose)
            };

            string secretKey = _configuration["Jwt:Key"]!;
            string issuer = _configuration["Jwt:Issuer"]!;
            string audience = _configuration["Jwt:Audience"]!;

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey) );
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                  issuer: issuer ,
                  audience: audience ,
                  claims: claims,
                  expires: DateTime.UtcNow.Add( expiration ) ,
                  signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    
    }

}
