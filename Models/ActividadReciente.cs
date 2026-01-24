namespace Panel.Models;

/// <summary>
/// Representa una actividad reciente del usuario para mostrar en el historial
/// </summary>
public class ActividadReciente
{
    public string Tipo { get; set; } = string.Empty; // "tarea_completada", "mensaje_enviado", etc.
    public string Icono { get; set; } = "check"; // nombre del icono
    public string Titulo { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string ColorIcono { get; set; } = "#6B7280"; // Gray por defecto
    
    /// <summary>
    /// Tiempo relativo desde que ocurrió la actividad
    /// </summary>
    public string TiempoRelativo
    {
        get
        {
            var diff = DateTime.Now - Fecha;
            
            if (diff.TotalMinutes < 1) return "Ahora mismo";
            if (diff.TotalMinutes < 60) return $"Hace {(int)diff.TotalMinutes} min";
            if (diff.TotalHours < 24) return $"Hace {(int)diff.TotalHours}h";
            if (diff.TotalDays < 2) return "Ayer";
            if (diff.TotalDays < 7) return $"Hace {(int)diff.TotalDays} días";
            
            return Fecha.ToString("dd MMM");
        }
    }
}
