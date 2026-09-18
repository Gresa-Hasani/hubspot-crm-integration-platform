using CrmIntegration.Application.Security;
using Xunit;

namespace CrmIntegration.UnitTests.Security;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Abcdefg1")]
    [InlineData("longerPassword9")]
    public void IsValid_AcceptsPasswordsMeetingThePolicy(string password) =>
        Assert.True(PasswordPolicy.IsValid(password));

    [Theory]
    [InlineData("short1")]
    [InlineData("nodigitshere")]
    [InlineData("12345678")]
    [InlineData("")]
    public void IsValid_RejectsPasswordsNotMeetingThePolicy(string password) =>
        Assert.False(PasswordPolicy.IsValid(password));
}
