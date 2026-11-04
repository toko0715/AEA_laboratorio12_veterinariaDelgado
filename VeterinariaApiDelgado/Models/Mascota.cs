using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace VeterinariaApiDelgado.Models;

[Index(nameof(PropietarioId), Name = "IX_Mascotas_PropietarioId")]
public partial class Mascota
{
    [Key]
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, MinimumLength = 1)]
    public string Nombre { get; set; } = null!;

    [Required(ErrorMessage = "La especie es obligatoria.")]
    [StringLength(50, MinimumLength = 1)]
    public string Especie { get; set; } = null!;

    [StringLength(100)]
    public string? Raza { get; set; }

    [Range(0, 100, ErrorMessage = "La edad debe estar entre 0 y 100 años.")]
    public int Edad { get; set; }

    [Column(TypeName = "decimal(6, 2)")]
    [Range(typeof(decimal), "0.01", "999.99", ErrorMessage = "El peso debe estar entre 0.01 y 999.99 kg.", ParseLimitsInInvariantCulture = true)]
    public decimal PesoKg { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Debe indicar un propietario válido.")]
    public int PropietarioId { get; set; }

    // Nullable para permitir POST con solo PropietarioId.
    [ForeignKey(nameof(PropietarioId))]
    [InverseProperty(nameof(Models.Propietario.Mascota))]
    public virtual Propietario? Propietario { get; set; }
}
