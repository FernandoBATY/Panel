using SQLite;
using System;

namespace Panel.Models;

public class Alerta
{
    // Identificación y prioridad
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [MaxLength(20)]
    [Indexed]
    public string Prioridad { get; set; } = "MEDIA"; 

    [MaxLength(100)]
    public string Titulo { get; set; } = string.Empty;

    // Contenido y fecha
    [MaxLength(2000)]
    public string Mensaje { get; set; } = string.Empty;

    [Indexed]
    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    public bool Vista { get; set; } = false;

    // Destinatario y tipo
    [Indexed]
    public int DestinatarioId { get; set; } 

    [MaxLength(30)]
    public string Tipo { get; set; } = "NOTIFICACION";

    // ============================================
    // RELACIÓN CON TAREA
    // ============================================
    [Indexed]
    [MaxLength(50)]
    public string? TareaRelacionadaId { get; set; }

    [MaxLength(30)]
    public string TipoAlertaTarea { get; set; } = "Manual";
    // Valores: Vencimiento, Retrasada, Asignacion, Completada, Comentario, Manual

    // Información adicional
    public bool AutoGenerada { get; set; } = false;
    public int? CreadoPorId { get; set; }
    public DateTime? FechaExpiracion { get; set; }

    // UI
    [MaxLength(20)]
    public string Icono { get; set; } = "campana";

    [MaxLength(10)]
    public string ColorHex { get; set; } = "#3B82F6";

    // ============================================
    // PROPIEDADES CALCULADAS
    // ============================================
    [Ignore]
    public bool TieneRelacionTarea => !string.IsNullOrEmpty(TareaRelacionadaId);

    [Ignore]
    public string TiempoRelativo
    {
        get
        {
            var diff = DateTime.Now - FechaCreacion;
            if (diff.TotalMinutes < 1) return "Ahora";
            if (diff.TotalMinutes < 60) return $"Hace {(int)diff.TotalMinutes}m";
            if (diff.TotalHours < 24) return $"Hace {(int)diff.TotalHours}h";
            if (diff.TotalDays < 7) return $"Hace {(int)diff.TotalDays}d";
            return FechaCreacion.ToString("dd MMM");
        }
    }

    [Ignore]
    public Color ColorAlerta => Color.FromArgb(ColorHex);

    [Ignore]
    public string IconoTipo => TipoAlertaTarea switch
    {
        "Vencimiento" => "campana",
        "Retrasada" => "advertencia",
        "Asignacion" => "copiar",
        "Completada" => "trofeo",
        "Comentario" => "chat",
        _ => Icono
    };

    [Ignore]
    public Color ColorTipo => TipoAlertaTarea switch
    {
        "Vencimiento" => Color.FromArgb("#F59E0B"),
        "Retrasada" => Color.FromArgb("#EF4444"),
        "Asignacion" => Color.FromArgb("#3B82F6"),
        "Completada" => Color.FromArgb("#10B981"),
        "Comentario" => Color.FromArgb("#8B5CF6"),
        _ => Color.FromArgb("#6B7280")
    };
}
