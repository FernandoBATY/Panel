namespace Panel.Models;

public class ContadorReporte
{
    // Datos base del contador
    public User Contador { get; set; } = new();
    
    // Métricas de tareas
    public int TotalTareas { get; set; }
    public int Completadas { get; set; }
    public int Pendientes { get; set; }
    
    // Métricas de eficiencia
    public double Eficiencia { get; set; }
    public string EficienciaTexto => $"{Eficiencia:P0}";
    public string Nombre => Contador.Name;
    
    // Tiempos trabajados
    public decimal TiempoTrabajado { get; set; }
    public string TiempoTrabajadoTexto => $"{TiempoTrabajado:0.0}h";
    public string TiempoSesion { get; set; } = "00:00:00";
    public string Username => Contador.Username;
    
    // Derivados para UI
    public double Porcentaje => TotalTareas > 0 ? (double)Completadas / TotalTareas : 0;
    public double BarHeight => Porcentaje * 150;
    public Color ColorEstado => Porcentaje >= 0.8 ? Color.FromArgb("#10B981") : 
                               Porcentaje >= 0.5 ? Color.FromArgb("#F59E0B") : 
                               Color.FromArgb("#EF4444"); 
}
