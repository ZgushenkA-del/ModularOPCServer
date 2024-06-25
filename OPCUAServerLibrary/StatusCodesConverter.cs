using ModuleLibrary;
using Opc.Ua;

namespace OPCUAServerLibrary;

public static class StatusCodesConverter
{
    public static StatusCode GetStatusFrom(string statusString)
    {
        var statusCode = StatusCodes.Good;
        switch (statusString)
        {
            case "Good":
                statusCode = StatusCodes.Good;
                break;
            case "Bad":
                statusCode = StatusCodes.Bad;
                break;
            default:
                break;
        }
        return statusCode;
    }
    public static StatusCode GetStatusFrom(StatusValues statusCode)
    {
        return statusCode switch
        {
            StatusValues.Good => (StatusCode)StatusCodes.Good,
            StatusValues.Bad => (StatusCode)StatusCodes.Bad,
            _ => (StatusCode)StatusCodes.Good,
        };
    }
}
