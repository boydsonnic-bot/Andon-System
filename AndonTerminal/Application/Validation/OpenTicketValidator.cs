using SharedLib.Contracts.Requests;
using System.Collections.Generic;

namespace AndonTerminal.Application.Validation;

public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; set; } = new List<string>();
}

public class OpenTicketValidator
{
    public ValidationResult Validate(OpenTicketRequest request)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(request.MachineName))
        {
            result.Errors.Add("MachineName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.StationName))
        {
            result.Errors.Add("StationName is required.");
        }

        return result;
    }
}