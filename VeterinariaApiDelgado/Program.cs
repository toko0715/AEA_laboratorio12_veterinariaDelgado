using Microsoft.EntityFrameworkCore;
using VeterinariaApiDelgado.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<VeterinariaContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("VeterinariaDB")
        ?? throw new InvalidOperationException("Configure ConnectionStrings:VeterinariaDB mediante secretos o variables de entorno.")));

var origenes = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5500"];
if (origenes.Length == 0 || origenes.Any(o =>
        !Uri.TryCreate(o, UriKind.Absolute, out var uri)
        || (uri.Scheme != "http" && uri.Scheme != "https")
        || uri.GetLeftPart(UriPartial.Authority) != o || o.Contains('*')))
    throw new InvalidOperationException("Cors:AllowedOrigins debe contener orígenes concretos, sin rutas ni comodines.");
builder.Services.AddCors(options => options.AddPolicy("ClienteVeterinaria", policy =>
    policy.WithOrigins(origenes).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("ClienteVeterinaria");
app.UseAuthorization();

// El laboratorio requiere OpenAPI y Swagger también en producción.
app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "Veterinaria API v1");
    options.RoutePrefix = "swagger";
});
app.MapControllers();
app.Run();
