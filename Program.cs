using KheyBackend.DBContext;
using KheyBackend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Email MailKit
builder.Services.AddScoped<IEmailService, EmailService>(); 

// CONFIGURACION DE MYSQL ENTITY FRAMEWORK
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");


builder.Services.AddDbContext<AppDbContext>(
    dbContextOptions => dbContextOptions
        .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
        // The following three options help with debugging, but should
        // be changed or removed for production.
        .LogTo(Console.WriteLine, LogLevel.Information)
        .EnableSensitiveDataLogging()
        .EnableDetailedErrors()
);


// SERVICIO DE AUTH DE JWT

var jwtkey      = builder.Configuration["Jwt:Key"];
var jwtIssuer   = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];


builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme )
        .AddJwtBearer( options => 
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true  ,
                ValidIssuer = jwtIssuer     ,
                ValidAudience = jwtAudience ,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes( jwtkey! ))
            };

            // una vez se valida el token extraemos el JTI
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    // Extraccion de JIT del token
                    var jti = context.Principal?.FindFirst( JwtRegisteredClaimNames.Jti )?.Value;

                    if( string.IsNullOrEmpty( jti ) )
                    {
                        context.Fail("El token no contiene un identificador JIT valido");
                        return;
                    }

                    // 2.- Acceso a la Database para evaluar RevokedTokens table
                    var dbcontext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

                    bool isRevoked = await dbcontext.RevokedTokens.AnyAsync(r => r.Jti == jti);

                    if( isRevoked )
                    {
                        context.Fail("Este token ha sido revokado (logout)");
                    }
                }
            };
        } 
        );

builder.Services.AddAuthorization();


builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// setup CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy(
       name: "_myAllowSpecificOrigins",
       policy =>
       {
           policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod(); // Angular frontend local 
       }


            
    );
}
);



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
// CORS

app.UseCors( "_myAllowSpecificOrigins" );

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
