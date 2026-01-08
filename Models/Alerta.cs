using SQLite;
using System;

namespace Panel.Models;

public class Alerta
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [MaxLength(20)]
    [Indexed]
    public string Prioridad { get; set; } = "MEDIA"; // "ALTA", "MEDIA", "BAJA"

    [MaxLength(2000)]
    public string Mensaje { get; set; } = string.Empty;

    [Indexed]
    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    public bool Vista { get; set; } = false;

    [Indexed]
    public int DestinatarioId { get; set; } // User.Id del contador

    [MaxLength(20)]
    public string Tipo { get; set; } = "NOTIFICACION"; // "ALERTA", "NOTIFICACION"
}
