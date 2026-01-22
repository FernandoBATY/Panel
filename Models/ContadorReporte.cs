namespace Panel.Models;

public class ContadorReporte
{
    public User Contador { get; set; } = new();
    public int TotalTareas { get; set; }
    public int Completadas { get; set; }
    public int Pendientes { get; set; }
    public double Eficiencia { get; set; } // 0.0 to 1.0
    public string EficienciaTexto => $"{Eficiencia:P0}";
    public string Nombre => Contador.Name;
    public decimal TiempoTrabajado { get; set; }
    public string TiempoTrabajadoTexto => $"{TiempoTrabajado:0.0}h";
    public string TiempoSesion { get; set; } = "00:00:00";
    public string Username => Contador.Username;
    
    // UI Helpers for Charts
    public double Porcentaje => TotalTareas > 0 ? (double)Completadas / TotalTareas : 0;
    public double BarHeight => Porcentaje * 150; // Max height 150px
    public Color ColorEstado => Porcentaje >= 0.8 ? Color.FromArgb("#10B981") : // Green
                               Porcentaje >= 0.5 ? Color.FromArgb("#F59E0B") : // Orange
                               Color.FromArgb("#EF4444"); // Red
}
