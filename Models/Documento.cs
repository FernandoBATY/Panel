using SQLite;
using System;

namespace Panel.Models;

public class Documento
{
    // Identificación y metadatos
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [MaxLength(255)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Tipo { get; set; } = string.Empty; 

    // Información de archivo
    public DateTime FechaSubida { get; set; }

    public long TamanoBytes { get; set; } = 0; 

    // Relaciones y origen
    [Indexed]
    public string TareaId { get; set; } = string.Empty; 

    [Indexed]
    public int SubidoPorId { get; set; } 

    [MaxLength(500)]
    public string RutaArchivo { get; set; } = string.Empty; 
}
