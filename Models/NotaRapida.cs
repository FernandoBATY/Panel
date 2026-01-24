using SQLite;

namespace Panel.Models;

/// <summary>
/// Notas rápidas personales del contador
/// </summary>
public class NotaRapida
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    [Indexed]
    public int UsuarioId { get; set; }
    
    [MaxLength(2000)]
    public string Contenido { get; set; } = string.Empty;
    
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    
    public DateTime FechaModificacion { get; set; } = DateTime.Now;
}
