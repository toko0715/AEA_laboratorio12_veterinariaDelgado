using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VeterinariaApiDelgado.Data;
using VeterinariaApiDelgado.Models;

namespace VeterinariaApiDelgado.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MascotasController(VeterinariaContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Mascota>>> GetTodas(
        [FromQuery] string? nombre, [FromQuery] string? especie,
        [FromQuery, Range(1, int.MaxValue)] int? propietarioId,
        [FromQuery, Range(0, 100)] int? edadMin,
        [FromQuery, Range(0, 100)] int? edadMax,
        CancellationToken cancellationToken)
    {
        if (edadMin.HasValue && edadMax.HasValue && edadMin > edadMax)
            return BadRequest(new { mensaje = "edadMin no puede ser mayor que edadMax." });
        var consulta = context.Mascotas.AsNoTracking().Include(m => m.Propietario).AsQueryable();
        if (!string.IsNullOrWhiteSpace(nombre))
            consulta = consulta.Where(m => m.Nombre.Contains(nombre.Trim()));
        if (!string.IsNullOrWhiteSpace(especie))
            consulta = consulta.Where(m => m.Especie == especie.Trim());
        if (propietarioId.HasValue)
            consulta = consulta.Where(m => m.PropietarioId == propietarioId.Value);
        if (edadMin.HasValue) consulta = consulta.Where(m => m.Edad >= edadMin.Value);
        if (edadMax.HasValue) consulta = consulta.Where(m => m.Edad <= edadMax.Value);
        return Ok(await consulta.OrderBy(m => m.Id).ToListAsync(cancellationToken));
    }

    [HttpGet("especies")]
    public async Task<ActionResult<IEnumerable<string>>> GetEspecies(CancellationToken cancellationToken)
    {
        return Ok(await context.Mascotas.AsNoTracking().Select(m => m.Especie)
            .Distinct().OrderBy(e => e).ToListAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<Mascota>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Mascota>> GetPorId(int id, CancellationToken cancellationToken)
    {
        var mascota = await context.Mascotas.AsNoTracking().Include(m => m.Propietario)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (mascota is null) return NotFound(new { mensaje = "La mascota no existe." });
        return Ok(mascota);
    }

    [HttpPost]
    [ProducesResponseType<Mascota>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Mascota>> Crear(Mascota mascota, CancellationToken cancellationToken)
    {
        if (mascota.Id != 0)
            return BadRequest(new { mensaje = "No envíe un Id al crear; lo genera la base de datos." });
        if (!await context.Propietarios.AnyAsync(p => p.Id == mascota.PropietarioId, cancellationToken))
            return BadRequest(new { mensaje = "El propietario indicado no existe." });
        mascota.Nombre = mascota.Nombre.Trim();
        mascota.Especie = mascota.Especie.Trim();
        mascota.Raza = mascota.Raza?.Trim();
        // Se vincula por la clave foránea, sin crear un propietario anidado.
        mascota.Propietario = null;
        context.Mascotas.Add(mascota);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException e) when (ErroresSql.EsRestriccion(e))
        {
            return BadRequest(new { mensaje = "El propietario ya no existe o los datos incumplen una restricción." });
        }
        return CreatedAtAction(nameof(GetPorId), new { id = mascota.Id }, mascota);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Actualizar(int id, Mascota datos, CancellationToken cancellationToken)
    {
        if (id != datos.Id)
            return BadRequest(new { mensaje = "El Id de la ruta debe coincidir con el del cuerpo." });
        var mascota = await context.Mascotas.FindAsync(new object[] { id }, cancellationToken);
        if (mascota is null) return NotFound(new { mensaje = "La mascota no existe." });
        if (!await context.Propietarios.AnyAsync(p => p.Id == datos.PropietarioId, cancellationToken))
            return BadRequest(new { mensaje = "El propietario indicado no existe." });
        mascota.Nombre = datos.Nombre.Trim();
        mascota.Especie = datos.Especie.Trim();
        mascota.Raza = datos.Raza?.Trim();
        mascota.Edad = datos.Edad;
        mascota.PesoKg = datos.PesoKg;
        mascota.PropietarioId = datos.PropietarioId;
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException e) when (ErroresSql.EsRestriccion(e))
        {
            return BadRequest(new { mensaje = "El propietario ya no existe o los datos incumplen una restricción." });
        }
        catch (DbUpdateConcurrencyException)
        {
            return NotFound(new { mensaje = "La mascota ya no existe." });
        }
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(int id, CancellationToken cancellationToken)
    {
        var mascota = await context.Mascotas.FindAsync(new object[] { id }, cancellationToken);
        if (mascota is null) return NotFound(new { mensaje = "La mascota no existe." });
        context.Mascotas.Remove(mascota);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            return NotFound(new { mensaje = "La mascota ya no existe." });
        }
        return NoContent();
    }
}
