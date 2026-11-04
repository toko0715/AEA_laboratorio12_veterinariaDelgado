using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace VeterinariaApiDelgado.Models;

[Index(nameof(Documento), Name = "UQ_Propietarios_Documento", IsUnique = true)]
public partial class Propietario
{
    [Key]
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, MinimumLength = 1)]
    public string Nombre { get; set; } = null!;

    [Required(ErrorMessage = "El documento es obligatorio.")]
    [StringLength(20, MinimumLength = 1)]
    public string Documento { get; set; } = null!;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [StringLength(20, MinimumLength = 1)]
    public string Telefono { get; set; } = null!;

    // Evita ciclos de JSON y altas anidadas; las mascotas tienen su propio endpoint.
    [JsonIgnore]
    [InverseProperty(nameof(Models.Mascota.Propietario))]
    public virtual ICollection<Mascota> Mascota { get; set; } = new List<Mascota>();
}
