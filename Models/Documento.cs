using SQLite;
using System;

namespace Panel.Models;

public class Documento
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [MaxLength(255)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Tipo { get; set; } = string.Empty; // "PDF", "Excel", "Word", "Otro"

    public DateTime FechaSubida { get; set; }

    public long TamanoBytes { get; set; } = 0; // Tamaño en bytes

    [Indexed]
    public string TareaId { get; set; } = string.Empty; // Foreign Key to Tarea.Id

    [Indexed]
    public int SubidoPorId { get; set; } // User.Id quien subió el documento

    [MaxLength(500)]
    public string RutaArchivo { get; set; } = string.Empty; // Ruta local del archivo
}
