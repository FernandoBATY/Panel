using SQLite;
using System;

namespace Panel.Models;

public class Tarea
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [MaxLength(200)]
    public string Titulo { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Descripcion { get; set; } = string.Empty;

    [Indexed]
    public DateTime FechaVencimiento { get; set; }

    public DateTime FechaAsignacion { get; set; }

    public DateTime? FechaCompletado { get; set; }

    [Indexed]
    public string Estado { get; set; } = "pendiente"; // "completada", "en-progreso", "pendiente", "retrasada"

    [Indexed]
    public string Prioridad { get; set; } = "Variable"; // "Prioritaria", "Variable"

    [Indexed]
    public string CategoriaKPI { get; set; } = "General"; // "Ingresos", "Egresos", "Declaraciones", "OpinionSAT", "EnvioPrevios"

    public decimal TiempoEstimado { get; set; }
    public decimal TiempoReal { get; set; }

    [Indexed]
    public int AsignadoAId { get; set; } // Foreign Key to User.Id (int)
}
