namespace Panel.Models;

/// <summary>
/// Representa el progreso de un día para la gráfica semanal
/// </summary>
public class ProgresoSemanalItem
{
    public DateTime Fecha { get; set; }
    public string DiaSemana { get; set; } = string.Empty; // "LU", "MA", etc.
    public int Cantidad { get; set; } // Tareas completadas ese día
    public double AlturaBarra { get; set; } // Altura proporcional de la barra
    public bool EsHoy { get; set; }
}
