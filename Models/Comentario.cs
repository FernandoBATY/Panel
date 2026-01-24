using SQLite;

namespace Panel.Models;

public class Comentario
{
    // Identificación
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // Relación con tarea
    [Indexed]
    public string TareaId { get; set; } = string.Empty;

    // Autor del comentario
    [Indexed]
    public int AutorId { get; set; }

    // Contenido del comentario
    [MaxLength(2000)]
    public string Contenido { get; set; } = string.Empty;

    // Fechas
    [Indexed]
    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    public DateTime? FechaEdicion { get; set; }

    public bool Editado { get; set; } = false;

    // Menciones en formato JSON: [1, 2, 3] (IDs de usuarios mencionados)
    [MaxLength(500)]
    public string Menciones { get; set; } = string.Empty;

    // Etiquetas mencionadas en formato JSON: [1, 2] (IDs de etiquetas)
    [MaxLength(500)]
    public string EtiquetasMencionadas { get; set; } = string.Empty;

    // Tipo de comentario
    [MaxLength(20)]
    public string Tipo { get; set; } = "Comentario";  // Comentario, Sistema, Actualización

    // Propiedades calculadas para UI
    [Ignore]
    public string AutorNombre { get; set; } = string.Empty;

    [Ignore]
    public string AutorFoto { get; set; } = string.Empty;

    [Ignore]
    public string TiempoRelativo => CalcularTiempoRelativo();

    [Ignore]
    public bool EsMio { get; set; }

    [Ignore]
    public Color ColorTipo => Tipo switch
    {
        "Sistema" => Color.FromArgb("#6B7280"),
        "Actualización" => Color.FromArgb("#3B82F6"),
        _ => Color.FromArgb("#10B981")
    };

    [Ignore]
    public string IconoTipo => Tipo switch
    {
        "Sistema" => "refresh",
        "Actualización" => "refresh",
        _ => "chat"
    };

    private string CalcularTiempoRelativo()
    {
        var diferencia = DateTime.Now - FechaCreacion;

        if (diferencia.TotalMinutes < 1)
            return "Ahora mismo";
        if (diferencia.TotalMinutes < 60)
            return $"Hace {(int)diferencia.TotalMinutes} min";
        if (diferencia.TotalHours < 24)
            return $"Hace {(int)diferencia.TotalHours} h";
        if (diferencia.TotalDays < 7)
            return $"Hace {(int)diferencia.TotalDays} días";

        return FechaCreacion.ToString("dd/MM/yyyy HH:mm");
    }
}
