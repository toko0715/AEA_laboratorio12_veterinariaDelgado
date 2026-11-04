using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VeterinariaApiDelgado.Data;
using VeterinariaApiDelgado.Models;

namespace VeterinariaApiDelgado.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PropietariosController(VeterinariaContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Propietario>>> GetTodos(
        [FromQuery] string? nombre, [FromQuery] string? documento,
        CancellationToken cancellationToken)
    {
        var consulta = context.Propietarios.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(nombre))
            consulta = consulta.Where(p => p.Nombre.Contains(nombre.Trim()));
        if (!string.IsNullOrWhiteSpace(documento))
            consulta = consulta.Where(p => p.Documento == documento.Trim());
        return Ok(await consulta.OrderBy(p => p.Id).ToListAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<Propietario>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Propietario>> GetPorId(int id, CancellationToken cancellationToken)
    {
        var propietario = await context.Propietarios.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (propietario is null) return NotFound(new { mensaje = "El propietario no existe." });
        return Ok(propietario);
    }

    [HttpGet("{id:int}/mascotas")]
    [ProducesResponseType<IEnumerable<Mascota>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<Mascota>>> GetMascotas(int id, CancellationToken cancellationToken)
    {
        if (!await context.Propietarios.AnyAsync(p => p.Id == id, cancellationToken))
            return NotFound(new { mensaje = "El propietario no existe." });
        var mascotas = await context.Mascotas.AsNoTracking().Include(m => m.Propietario)
            .Where(m => m.PropietarioId == id).OrderBy(m => m.Id)
            .ToListAsync(cancellationToken);
        return Ok(mascotas);
    }

    [HttpPost]
    [ProducesResponseType<Propietario>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Propietario>> Crear(Propietario propietario, CancellationToken cancellationToken)
    {
        if (propietario.Id != 0)
            return BadRequest(new { mensaje = "No envíe un Id al crear; lo genera la base de datos." });
        propietario.Nombre = propietario.Nombre.Trim();
        propietario.Documento = propietario.Documento.Trim();
        propietario.Telefono = propietario.Telefono.Trim();
        if (await context.Propietarios.AnyAsync(p => p.Documento == propietario.Documento, cancellationToken))
            return Conflict(new { mensaje = "Ya existe un propietario con ese documento." });
        context.Propietarios.Add(propietario);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException e) when (ErroresSql.EsDuplicado(e))
        {
            return Conflict(new { mensaje = "Ya existe un propietario con ese documento." });
        }
        return CreatedAtAction(nameof(GetPorId), new { id = propietario.Id }, propietario);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(int id, Propietario datos, CancellationToken cancellationToken)
    {
        if (id != datos.Id)
            return BadRequest(new { mensaje = "El Id de la ruta debe coincidir con el del cuerpo." });
        var propietario = await context.Propietarios.FindAsync(new object[] { id }, cancellationToken);
        if (propietario is null) return NotFound(new { mensaje = "El propietario no existe." });
        var documento = datos.Documento.Trim();
        if (await context.Propietarios.AnyAsync(p => p.Id != id && p.Documento == documento, cancellationToken))
            return Conflict(new { mensaje = "Ya existe un propietario con ese documento." });
        propietario.Nombre = datos.Nombre.Trim();
        propietario.Documento = documento;
        propietario.Telefono = datos.Telefono.Trim();
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException e) when (ErroresSql.EsDuplicado(e))
        {
            return Conflict(new { mensaje = "Ya existe un propietario con ese documento." });
        }
        catch (DbUpdateConcurrencyException)
        {
            return NotFound(new { mensaje = "El propietario ya no existe." });
        }
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Eliminar(int id, CancellationToken cancellationToken)
    {
        var propietario = await context.Propietarios.FindAsync(new object[] { id }, cancellationToken);
        if (propietario is null) return NotFound(new { mensaje = "El propietario no existe." });
        if (await context.Mascotas.AnyAsync(m => m.PropietarioId == id, cancellationToken))
            return Conflict(new { mensaje = "No se puede eliminar un propietario que tiene mascotas." });
        context.Propietarios.Remove(propietario);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException e) when (ErroresSql.EsRestriccion(e))
        {
            return Conflict(new { mensaje = "No se puede eliminar un propietario que tiene mascotas." });
        }
        catch (DbUpdateConcurrencyException)
        {
            return NotFound(new { mensaje = "El propietario ya no existe." });
        }
        return NoContent();
    }
}
