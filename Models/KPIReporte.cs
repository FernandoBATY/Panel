namespace Panel.Models;

public class KPIReporte
{
    // Datos base del KPI
    public string Categoria { get; set; } = string.Empty;
    public int TotalTareas { get; set; }
    public int Completadas { get; set; }
    public double Porcentaje { get; set; }
    
    // Representaciones para UI
    public string PorcentajeTexto => $"{Porcentaje:P0}";
    public string ColorEstado => Porcentaje >= 0.8 ? "#10B981" : (Porcentaje >= 0.5 ? "#F59E0B" : "#EF4444");
    public string Detalle => $"{Completadas}/{TotalTareas}";
}
