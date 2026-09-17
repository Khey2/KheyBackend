using KheyBackend.DBContext;
using KheyBackend.DTOS;
using KheyBackend.Models;
using KheyBackend.Services;
using Microsoft.AspNetCore.Authorization;
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
                return BadRequest(new { message = "El email ya esta registrado" });
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
                purpose: "verify_account"
            );

            string frontend_url = _configuration["Jwt:Audience"]!;
            string emailHtmlBody = $"<div> <p> Hola { newUser.Username } este es el link de verificacion <a href='{ frontend_url }/verify-account?token={jwt_token}' > Confirmar cuenta </a> </p> </div>";

            await _emailService.SendEmailAsync( newUser.Email, "Khey verificacion de cuenta" , emailHtmlBody  );

            return Ok(new
            {
                message = "Usuario registrado correctamente",
                jwt     = jwt_token
            });
        }


        [HttpPost("login")]
        public async Task<IActionResult> login( LoginRequestDTO data  )
        {
            string email = data.email;
            string password = data.password;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == data.email);

            if (user == null)
            {
                return Unauthorized(new { message = "Credenciales incorrectas." });
            }

            bool isValidPassword = BC.Verify( password , user.PasswordHash);

            if (!isValidPassword)
            {
                return Unauthorized(new { message = "Credenciales incorrectas." });
            }

            if ( user.IsEmailVerified == false )
            {
                return Unauthorized(new { message = "Cuenta no verificada." });
            }

            // generar token de la sesion
            string jwt_token = GenerateJwtToken(user.Id, user.Username, TimeSpan.FromDays(7), "session");

            return Ok(new
                {
                    message = "Inicio de sesión exitoso.",
                    token = jwt_token ,
                    user = new {
                        id = user.Id ,
                        username = user.Username ,
                        email = user.Email
                    }
                });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> logout()
        {
            string authorizationHeader = Request.Headers["Authorization"].ToString();

            // 2. Limpiar el prefijo "Bearer " para quedarte solo con el string del token
            string rawToken = authorizationHeader.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim();

            Console.WriteLine( rawToken );
            
            return Ok(new { message = "Sesión cerrada correctamente." });
        }

        [HttpPost("verify-email")]
        [Authorize]
        public async Task<IActionResult> verifyEmail( )
        {
            var userId      = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username    = User.FindFirst( "username" )?.Value;
            var purpose     = User.FindFirst( "purpose" )?.Value;
            var jti         = User.FindFirst("jti")?.Value;
            var expClaim    = User.FindFirst( "exp" )?.Value;

            // 2. Convertir el timestamp 'exp' a DateTime
            long expUnix = long.Parse(expClaim!);
            DateTime expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;

            if ( purpose != "verify_account" )
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Verificacion de cuenta fallida: El token proporcionado no es válido para esta acción." });
            }

            if (!int.TryParse(userId, out int parsedUserId))
            {
                return BadRequest(new { message = "Verificacion de cuenta fallida: El ID de usuario no es válido." });
            }


            // Buscar que exista un usuario registrado bajo estos 3 parametros y verificarlo
            var user = await _context.Users.FirstOrDefaultAsync( u =>
                u.Id       == parsedUserId  &&
                u.Username == username
            );

            if (user == null)
            {
                return NotFound(new { message = "Verificacion de cuenta fallida: Usuario no encontrado." });
            }

            // verificacion del email
            user.IsEmailVerified = true;

            // Revokar token ( añadirlo a la tabla  )
            var revokedToken = new RevokedToken
            {             
                Jti = jti! ,
                ExpiresAt = expiresAt ,
                RevokedAt = DateTime.UtcNow                 
            };

            await _context.RevokedTokens.AddAsync(revokedToken);

            await _context.SaveChangesAsync();


            return Ok(new { message = "Cuenta verificada correctamente." });
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
