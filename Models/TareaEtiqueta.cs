using SQLite;

namespace Panel.Models;

public class TareaEtiqueta
{
    // Identificación
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // Relaciones
    [Indexed]
    public string TareaId { get; set; } = string.Empty;

    [Indexed]
    public int EtiquetaId { get; set; }

    // Metadatos
    public DateTime FechaAsignacion { get; set; } = DateTime.Now;

    public int AsignadoPorId { get; set; }

    // Propiedades calculadas para UI (se cargan desde Etiqueta)
    [Ignore]
    public string NombreEtiqueta { get; set; } = string.Empty;

    [Ignore]
    public string ColorEtiqueta { get; set; } = "#3B82F6";

    [Ignore]
    public string IconoEtiqueta { get; set; } = "tag";
}
