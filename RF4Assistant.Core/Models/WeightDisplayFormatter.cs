namespace RF4AssistantPro.Models;

public static class WeightDisplayFormatter
{
    public static string Format(decimal weightKg)
    {
        if (weightKg < 1m)
        {
            return $"{weightKg * 1000m:0.##} г";
        }

        return $"{weightKg:0.###} кг";
    }
}