using System.Globalization;

namespace Panel.Converters;

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

public class EqualConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        
        // Handle int comparison specifically
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

public class PriorityToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string prioridad)
        {
            return prioridad == "Prioritaria" ? Color.FromArgb("#DC2626") : Color.FromArgb("#F59E0B");
        }
        return Color.FromArgb("#F59E0B"); // Default to Variable color
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

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

public class StatusBackgroundColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string estado)
        {
            return estado switch
            {
                "completada" => Color.FromArgb("#DCFCE7"), // Green100
                "en-progreso" => Color.FromArgb("#DBEAFE"), // Blue100
                "pendiente" => Color.FromArgb("#F3F4F6"), // Gray100
                "retrasada" => Color.FromArgb("#FEE2E2"), // Red100
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

public class StatusTextColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string estado)
        {
            return estado switch
            {
                "completada" => Color.FromArgb("#065F46"), // Green800
                "en-progreso" => Color.FromArgb("#1E3A8A"), // Blue900
                "pendiente" => Color.FromArgb("#374151"), // Gray700
                "retrasada" => Color.FromArgb("#991B1B"), // Red800
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

public class AlertTypeToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string tipo)
        {
            return tipo.ToUpper() switch
            {
                "ALERTA" => Color.FromArgb("#EF4444"), // Red500
                "NOTIFICACION" => Color.FromArgb("#3B82F6"), // Blue500
                "MENSAJE" => Color.FromArgb("#6B7280"), // Gray500
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

public class AlertTypeToBgColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string tipo)
        {
            return tipo.ToUpper() switch
            {
                "ALERTA" => Color.FromArgb("#FEE2E2"), // Red100
                "NOTIFICACION" => Color.FromArgb("#DBEAFE"), // Blue100
                "MENSAJE" => Color.FromArgb("#F3F4F6"), // Gray100
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
