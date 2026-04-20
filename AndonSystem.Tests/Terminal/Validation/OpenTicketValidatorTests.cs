using FluentAssertions;
using Xunit;
using SharedLib.Contracts.Requests;
using AndonTerminal.Application.Validation;

namespace AndonSystem.Tests.Terminal.Validation;

public class OpenTicketValidatorTests
{
    private readonly OpenTicketValidator _validator;

    public OpenTicketValidatorTests()
    {
        _validator = new OpenTicketValidator();
    }

    [Fact]
    public void UNIT_VAL_001_Validate_MissingStationName_ReturnsError()
    {
        var request = new OpenTicketRequest { MachineName = "M1", StationName = null, ErrorCode = "E1" };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("StationName"));
    }

    [Fact]
    public void UNIT_VAL_002_Validate_MissingMachineName_ReturnsError()
    {
        var request = new OpenTicketRequest { MachineName = "", StationName = "S1", ErrorCode = "E1" };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("MachineName"));
    }

    [Fact]
    public void UNIT_VAL_003_Validate_AllFieldsValid_Passes()
    {
        var request = new OpenTicketRequest { MachineName = "M1", StationName = "S1", ErrorCode = "E1" };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}