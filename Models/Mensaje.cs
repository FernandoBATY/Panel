using SQLite;
using System;

namespace Panel.Models;

public class Mensaje
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string De { get; set; } = string.Empty; // "admin" or User Id

    public string Para { get; set; } = string.Empty; // User Id or "todos"

    [MaxLength(2000)]
    public string Contenido { get; set; } = string.Empty;

    [Indexed]
    public DateTime MarcaTiempo { get; set; }

    public string Tipo { get; set; } = "mensaje"; // "mensaje", "alerta", "notificacion"

    public bool Leido { get; set; } = false;

    [Ignore]
    public bool EsMio { get; set; }

    [Ignore]
    public string DeNombre { get; set; } = string.Empty;

    [Ignore]
    public string ParaNombre { get; set; } = string.Empty;
}
