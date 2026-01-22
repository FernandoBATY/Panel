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

    [MaxLength(50)]
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

    [MaxLength(20)]
    public string Tipo { get; set; } = "NOTIFICACION"; 
}
