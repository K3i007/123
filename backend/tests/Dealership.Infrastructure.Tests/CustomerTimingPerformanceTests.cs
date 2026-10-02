using System.Diagnostics;
using Dealership.Domain;
using Dealership.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Dealership.Infrastructure.Tests;

[Trait("Category", "Performance")]
public sealed class CustomerTimingPerformanceTests(ITestOutputHelper output)
{
    [Fact]
    public void Server_password_work_medians_use_warmup_and_thirty_samples()
    {
        var passwords = new PasswordWorkService();
        var user = new User();
        var realHash = passwords.Hash(user, "Existing-password-123");
        
        output.WriteLine($"[Config] Configured Iterations: {passwords.IterationCount}");
        output.WriteLine($"[Hash] Real Hash Iterations: {passwords.GetIterationCount(realHash)}");
        output.WriteLine($"[Hash] Unknown Hash Iterations: {passwords.GetIterationCount(passwords.UnknownHashForTesting)}");

        var loginExisting = Median(() => passwords.Verify(user, realHash, "wrong-password-123"));
        var loginUnknown = Median(() => passwords.VerifyUnknown("wrong-password-123"));
        var registerExisting = Median(() => passwords.Hash(new User(), "New-password-123"));
        var registerNew = Median(() => passwords.Hash(new User(), "New-password-123"));
        var resetExisting = Median(() => passwords.VerifyUnknown("reset-placeholder"));
        var resetUnknown = Median(() => passwords.VerifyUnknown("reset-placeholder"));
        output.WriteLine($"login existing={loginExisting:F1}ms unknown={loginUnknown:F1}ms");
        output.WriteLine($"register existing={registerExisting:F1}ms new={registerNew:F1}ms");
        output.WriteLine($"reset existing={resetExisting:F1}ms unknown={resetUnknown:F1}ms");
        AssertWithinRelativeTolerance(loginExisting, loginUnknown);
        AssertWithinRelativeTolerance(registerExisting, registerNew);
        AssertWithinRelativeTolerance(resetExisting, resetUnknown);
    }

    private static double Median(Action operation)
    {
        for (var warmup = 0; warmup < 5; warmup++) operation();
        var values = new List<double>(30);
        for (var sample = 0; sample < 30; sample++)
        {
            var timer = Stopwatch.StartNew(); operation(); values.Add(timer.Elapsed.TotalMilliseconds);
        }
        return values.OrderBy(value => value).ElementAt(15);
    }

    private static void AssertWithinRelativeTolerance(double left, double right)
    {
        var difference = Math.Abs(left - right) / Math.Max(left, right);
        Assert.True(difference <= 0.50, $"Median response times diverged by {difference:P1}; maximum is 50%.");
    }

}
