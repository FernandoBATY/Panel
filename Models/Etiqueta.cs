using SQLite;

namespace Panel.Models;

public class Etiqueta
{
    // Identificación
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    // Información básica
    [MaxLength(50)]
    [Unique]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(7)]  // Color hex: #RRGGBB
    public string ColorHex { get; set; } = "#3B82F6";

    [MaxLength(200)]
    public string Descripcion { get; set; } = string.Empty;

    // Icono (nombre del icono SVG)
    [MaxLength(20)]
    public string Icono { get; set; } = "tag";

    // Metadatos
    [Indexed]
    public int CreadorId { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    public bool Activa { get; set; } = true;

    // Propiedades calculadas para UI
    [Ignore]
    public int TotalUsos { get; set; }

    [Ignore]
    public string CreadorNombre { get; set; } = string.Empty;

    [Ignore]
    public Color ColorEtiqueta => Color.FromArgb(ColorHex);

    [Ignore]
    public string NombreCompleto => $"{Icono} {Nombre}";

    [Ignore]
    public bool EstaSeleccionada { get; set; }
}
