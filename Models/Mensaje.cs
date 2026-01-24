using SQLite;
using System;

namespace Panel.Models;

public class Mensaje
{
    // Identificación y direcciones
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string De { get; set; } = string.Empty; 

    public string Para { get; set; } = string.Empty; 

    // Contenido y metadatos
    [MaxLength(2000)]
    public string Contenido { get; set; } = string.Empty;

    [Indexed]
    public DateTime MarcaTiempo { get; set; }

    public string Tipo { get; set; } = "mensaje"; 

    public bool Leido { get; set; } = false;

    // Propiedades derivadas para UI
    [Ignore]
    public bool EsMio { get; set; }

    [Ignore]
    public string DeNombre { get; set; } = string.Empty;

    [Ignore]
    public string ParaNombre { get; set; } = string.Empty;

    // Para agrupación por fecha
    [Ignore]
    public string FechaAgrupacion => MarcaTiempo.Date == DateTime.Today 
        ? "Hoy" 
        : MarcaTiempo.Date == DateTime.Today.AddDays(-1) 
            ? "Ayer" 
            : MarcaTiempo.ToString("dddd, dd MMMM");

    [Ignore]
    public bool EsPrimerMensajeDelDia { get; set; }
}
