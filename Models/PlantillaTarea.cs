using SQLite;

namespace Panel.Models;

public class PlantillaTarea
{
    // Identificación
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // Información básica
    [MaxLength(200)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string DescripcionBase { get; set; } = string.Empty;

    // Categorización y tiempo
    [MaxLength(50)]
    [Indexed]
    public string CategoriaKPI { get; set; } = "General";

    public decimal TiempoEstimadoHoras { get; set; } = 1;

    [MaxLength(20)]
    public string Prioridad { get; set; } = "Media";  // Baja, Media, Alta, Urgente

    // Programación recurrente
    [MaxLength(20)]
    [Indexed]
    public string Frecuencia { get; set; } = "Manual";  // Manual, Diaria, Semanal, Mensual, Trimestral

    public int DiaEjecucion { get; set; } = 1;  // Día del mes o semana para ejecución

    public int DiasAnticipacion { get; set; } = 0;  // Días antes del vencimiento para crear

    // Metadatos
    [Indexed]
    public int CreadorId { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    public DateTime? UltimaEjecucion { get; set; }

    public bool Activa { get; set; } = true;

    // Checklist en formato JSON: ["Paso 1", "Paso 2", "Paso 3"]
    [MaxLength(2000)]
    public string Checklist { get; set; } = string.Empty;

    // Asignación predeterminada (0 = sin asignar)
    public int AsignadoPorDefectoId { get; set; } = 0;

    // Propiedades calculadas para UI
    [Ignore]
    public string CreadorNombre { get; set; } = string.Empty;

    [Ignore]
    public string AsignadoNombre { get; set; } = string.Empty;

    [Ignore]
    public string FrecuenciaTexto => Frecuencia switch
    {
        "Diaria" => "Todos los días",
        "Semanal" => $"Cada semana (día {DiaEjecucion})",
        "Mensual" => $"Cada mes (día {DiaEjecucion})",
        "Trimestral" => "Cada 3 meses",
        _ => "Creación manual"
    };

    [Ignore]
    public Color ColorPrioridad => Prioridad switch
    {
        "Urgente" => Color.FromArgb("#EF4444"),
        "Alta" => Color.FromArgb("#F59E0B"),
        "Media" => Color.FromArgb("#3B82F6"),
        _ => Color.FromArgb("#6B7280")
    };

    [Ignore]
    public string IconoFrecuencia => Frecuencia switch
    {
        "Diaria" => "D",
        "Semanal" => "S",
        "Mensual" => "M",
        "Trimestral" => "T",
        _ => "-"
    };
}
