using PORMS.API.Services;
using Xunit;

namespace PORMS.Tests.Unit;

public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData("short", false)]
    [InlineData("alllowercase1!", false)]
    [InlineData("ALLUPPERCASE1!", false)]
    [InlineData("NoNumber!", false)]
    [InlineData("NoSpecial1", false)]
    [InlineData("Strong@2026!", true)]
    public void IsStrong_EnforcesAllPasswordRequirements(string password, bool expected)
    {
        Assert.Equal(expected, PasswordPolicy.IsStrong(password));
    }
}
