using SQLite;
using System;

namespace Panel.Models;

public class Tarea
{
    // Identificación y descripción
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [MaxLength(200)]
    public string Titulo { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Descripcion { get; set; } = string.Empty;

    // Fechas clave
    [Indexed]
    public DateTime FechaVencimiento { get; set; }

    public DateTime FechaAsignacion { get; set; }

    public DateTime? FechaCompletado { get; set; }

    // Estado y categorización
    [Indexed]
    public string Estado { get; set; } = "pendiente"; 

    [Indexed]
    public string Prioridad { get; set; } = "Variable"; 

    [Indexed]
    public string CategoriaKPI { get; set; } = "General"; 

    // Tiempos estimado y real
    public decimal TiempoEstimado { get; set; }
    public decimal TiempoReal { get; set; }

    // Asignación
    [Indexed]
    public int AsignadoAId { get; set; }
    
    // Propiedad calculada para mostrar el nombre del usuario asignado (no se guarda en BD)
    [Ignore]
    public string AsignadoANombre { get; set; } = string.Empty;
}
