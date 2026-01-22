using System.Globalization;

namespace Panel.Converters;

// Conversor de estado a booleano
public class StatusToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status == "completada";
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked)
        {
            return "completada";
        }
        return "pendiente"; 
    }
}

// Conversor de entero a booleano
public class IntToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            if (parameter is string paramString && int.TryParse(paramString, out int targetValue))
            {
                return intValue == targetValue;
            }
            return intValue > 0;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Comparación de igualdad genérica
public class EqualConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        
        if (value is int intValue && parameter is string paramString && int.TryParse(paramString, out int targetValue))
        {
            return intValue == targetValue;
        }

        return value.ToString() == parameter.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Inversión de valores booleanos
public class InverseBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}

// Validación de texto no vacío
public class StringNotEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrWhiteSpace(value as string);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Conversión de prioridad a color
public class PriorityToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string prioridad)
        {
            return prioridad == "Prioritaria" ? Color.FromArgb("#DC2626") : Color.FromArgb("#F59E0B");
        }
        return Color.FromArgb("#F59E0B"); 
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Indicadores de prioridad
public class IsPrioritariaConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string prioridad && prioridad == "Prioritaria";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class IsVariableConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string prioridad && prioridad == "Variable";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Colores de estado de tarea
public class StatusToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string estado)
        {
            return estado switch
            {
                "completada" => Color.FromArgb("#10B981"),
                "en-progreso" => Color.FromArgb("#3B82F6"),
                "pendiente" => Color.FromArgb("#F59E0B"),
                "retrasada" => Color.FromArgb("#EF4444"),
                _ => Color.FromArgb("#6B7280")
            };
        }
        return Color.FromArgb("#6B7280");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Fondos según estado de tarea
public class StatusBackgroundColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string estado)
        {
            return estado switch
            {
                "completada" => Color.FromArgb("#DCFCE7"), 
                "en-progreso" => Color.FromArgb("#DBEAFE"), 
                "pendiente" => Color.FromArgb("#F3F4F6"), 
                "retrasada" => Color.FromArgb("#FEE2E2"), 
                _ => Color.FromArgb("#F3F4F6")
            };
        }
        return Color.FromArgb("#F3F4F6");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Colores de texto por estado
public class StatusTextColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string estado)
        {
            return estado switch
            {
                "completada" => Color.FromArgb("#065F46"), 
                "en-progreso" => Color.FromArgb("#1E3A8A"), 
                "pendiente" => Color.FromArgb("#374151"), 
                "retrasada" => Color.FromArgb("#991B1B"), 
                _ => Color.FromArgb("#374151")
            };
        }
        return Color.FromArgb("#374151");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Colores para tipos de alerta
public class AlertTypeToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string tipo)
        {
            return tipo.ToUpper() switch
            {
                "ALERTA" => Color.FromArgb("#EF4444"), 
                "NOTIFICACION" => Color.FromArgb("#3B82F6"), 
                "MENSAJE" => Color.FromArgb("#6B7280"), 
                _ => Color.FromArgb("#6B7280")
            };
        }
        return Color.FromArgb("#6B7280");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Fondos para tipos de alerta
public class AlertTypeToBgColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string tipo)
        {
            return tipo.ToUpper() switch
            {
                "ALERTA" => Color.FromArgb("#FEE2E2"), 
                "NOTIFICACION" => Color.FromArgb("#DBEAFE"),
                "MENSAJE" => Color.FromArgb("#F3F4F6"), 
                _ => Color.FromArgb("#F3F4F6")
            };
        }
        return Color.FromArgb("#F3F4F6");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Control de visibilidad por booleano
public class IsVisibleIfTrueConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Obtención de iniciales a partir de nombre
public class StringToInitialsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string name && !string.IsNullOrWhiteSpace(name))
        {
            var parts = name.Trim().Split(' ');
            if (parts.Length > 0)
            {
                if (parts.Length > 1)
                    return $"{parts[0][0]}{parts[1][0]}".ToUpper();
                else
                    return $"{name[0]}".ToUpper();
            }
        }
        return "?";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


// Selección de texto según booleano
public class BoolToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string paramString)
        {
            var parts = paramString.Split('|');
            if (parts.Length == 2)
            {
                return boolValue ? parts[0] : parts[1];
            }
        }
        return parameter?.ToString() ?? "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Conversión de ruta de archivo a ImageSource
public class FileToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrWhiteSpace(path))
        {
            if (File.Exists(path))
            {
                return ImageSource.FromFile(path);
            }
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
