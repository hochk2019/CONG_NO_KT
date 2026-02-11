using Ops.Shared.Config;

namespace Ops.Tests;

public class AppPoolNameValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("DefaultAppPool")]
    [InlineData("Golden Logistics Pool")]
    [InlineData("Pool_1-Prod")]
    [InlineData("Pool.(Test)")]
    [InlineData("Kế toán số 1")]
    public void TryValidate_AllowsSupportedNames(string name)
    {
        var ok = AppPoolNameValidator.TryValidate(name, out var error);

        Assert.True(ok);
        Assert.True(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("Pool/Name")]
    [InlineData("Pool\\Name")]
    [InlineData("Pool:Name")]
    [InlineData("Pool\"Name")]
    public void TryValidate_RejectsInvalidCharacters(string name)
    {
        var ok = AppPoolNameValidator.TryValidate(name, out var error);

        Assert.False(ok);
        Assert.Equal(AppPoolNameValidator.InvalidMessage, error);
    }

    [Fact]
    public void TryValidate_RejectsTooLongName()
    {
        var name = new string('a', AppPoolNameValidator.MaxLength + 1);

        var ok = AppPoolNameValidator.TryValidate(name, out var error);

        Assert.False(ok);
        Assert.Equal(AppPoolNameValidator.LengthMessage, error);
    }
}
