using ScanEventWorker.Domain;

namespace ScanEventWorker.Tests.Domain;

public class ResultTests
{
    [Fact]
    public void Success_ErrorAccess_ThrowsInvalidOperationException()
    {
        var result = Result<bool>.Success(true);

        _ = Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Failure_ValueAccess_ThrowsInvalidOperationException()
    {
        var result = Result<bool>.Failure("some error");

        _ = Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
