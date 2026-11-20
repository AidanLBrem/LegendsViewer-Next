using LegendsViewer.Backend.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendsViewer.Backend.Tests.Logging;

[TestClass]
public class SimpleLogFormatterTests
{
    [TestMethod]
    public void IncludesExceptionAndStackDetailsForFailedWorldImport()
    {
        var failure = new InvalidOperationException("A large XML document could not be parsed");
        var entry = new LogEntry<string>(
            LogLevel.Error,
            "LegendsViewer.Backend.Controllers.BookmarkController",
            new EventId(0),
            "Failed to load world export",
            failure,
            (message, _) => message);
        using var writer = new StringWriter();

        new SimpleLogFormatter().Write(in entry, null, writer);
        string output = writer.ToString();

        StringAssert.Contains(output, "Failed to load world export");
        StringAssert.Contains(output, "InvalidOperationException");
        StringAssert.Contains(output, failure.Message);
    }

    [TestMethod]
    public void KeepsRoutineLogMessagesUnchanged()
    {
        var entry = new LogEntry<string>(
            LogLevel.Information,
            "LegendsViewer.Backend.Program",
            new EventId(0),
            "World loaded",
            null,
            (message, _) => message);
        using var writer = new StringWriter();

        new SimpleLogFormatter().Write(in entry, null, writer);

        Assert.AreEqual("World loaded" + Environment.NewLine, writer.ToString());
    }
}
