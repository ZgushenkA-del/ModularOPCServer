namespace ModuleLibrary;

public enum StatusValues
{
    Good,
    Bad,
    Warning
}

public static class StatusCodeHandler
{
    public static StatusValues GetStatusCode(string code)
    {
        return code.ToLower() switch
        {
            ("good") => StatusValues.Good,
            ("1") => StatusValues.Good,
            ("bad") => StatusValues.Bad,
            ("2") => StatusValues.Bad,
            ("warning") => StatusValues.Warning,
            ("3") => StatusValues.Warning,
            _ => StatusValues.Warning,
        };
    }
}
