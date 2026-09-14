var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

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

app.UseAuthorization();

app.MapControllers();

app.Run();
