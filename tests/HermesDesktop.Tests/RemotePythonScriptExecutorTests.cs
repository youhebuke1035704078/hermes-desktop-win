using HermesDesktop.Services;
using Xunit;

namespace HermesDesktop.Tests;

public class RemotePythonScriptExecutorTests
{
    [Fact]
    public void PythonStringLiteral_EscapesQuotesAndBackslashes()
    {
        var literal = RemotePythonScriptExecutor.PythonStringLiteral("""{"path":"C:\\temp\\it's-ok"}""");

        Assert.StartsWith("'", literal);
        Assert.EndsWith("'", literal);
        Assert.Contains("""C:\\\\temp\\\\it\'s-ok""", literal);
    }
}
