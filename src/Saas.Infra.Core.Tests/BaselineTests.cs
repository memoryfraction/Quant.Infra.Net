namespace Saas.Infra.Core.Tests;

/// <summary>
/// Baseline tests to verify test infrastructure is working correctly.
/// </summary>
public class BaselineTests
{
    [Fact]
    public void TestInfrastructure_ShouldWork()
    {
        // Arrange & Act
        var result = true;

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void InvalidTokenException_CanBeInstantiated()
    {
        // Arrange & Act
        var exception = new InvalidTokenException();

        // Assert
        Assert.NotNull(exception);
    }

    [Fact]
    public void JwtTokenResponse_CanBeInstantiated()
    {
        // Arrange & Act
        var response = new JwtTokenResponse();

        // Assert
        Assert.NotNull(response);
    }
}
