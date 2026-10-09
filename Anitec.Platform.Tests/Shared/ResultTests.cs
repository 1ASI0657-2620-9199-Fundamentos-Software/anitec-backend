using Anitec.Platform.Livestock.Domain.Model;
using Anitec.Platform.Shared.Application.Model;

namespace Anitec.Platform.Tests.Shared;

public class ResultTests
{
    [Fact]
    public void Success_ExposesValue_AndHasNoError()
    {
        var result = Result<string>.Success("ok");

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal("ok", result.Value);
        Assert.Null(result.Error);
        Assert.Equal(string.Empty, result.Message);
    }

    [Fact]
    public void Failure_ExposesErrorAndMessage_AndHasNoValue()
    {
        var result = Result<string>.Failure(LivestockError.AnimalNotFound, "Animal not found.");

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(LivestockError.AnimalNotFound, result.Error);
        Assert.Equal("Animal not found.", result.Message);
    }

    [Fact]
    public void NonGenericSuccess_IsSuccessful()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public void NonGenericFailure_CarriesErrorEnumAndMessage()
    {
        var result = Result.Failure(LivestockError.CorralNotFound, "Corral not found.");

        Assert.True(result.IsFailure);
        Assert.Equal(LivestockError.CorralNotFound, result.Error);
        Assert.Equal("Corral not found.", result.Message);
    }
}
