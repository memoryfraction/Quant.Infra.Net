using FsCheck;
using FsCheck.Xunit;

namespace Saas.Infra.Core.Tests;

/// <summary>
/// Baseline property-based tests to verify FsCheck is configured correctly.
/// </summary>
public class FsCheckBaselineTests
{
    [Property(MaxTest = 100)]
    public Property StringConcatenation_IsAssociative(string a, string b, string c)
    {
        // Property: (a + b) + c == a + (b + c)
        var left = (a + b) + c;
        var right = a + (b + c);
        
        return (left == right).ToProperty();
    }

    [Property(MaxTest = 100)]
    public Property IntegerAddition_IsCommutative(int a, int b)
    {
        // Property: a + b == b + a
        return (a + b == b + a).ToProperty();
    }
}
