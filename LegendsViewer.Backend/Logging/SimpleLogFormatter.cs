using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace LegendsViewer.Backend.Logging;
public class SimpleLogFormatter : ConsoleFormatter
{
    public SimpleLogFormatter() : base(nameof(SimpleLogFormatter))
    {
    }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        // Keep normal console output compact, but never hide errors.
        // World import logs the original exception through ILogger.LogError;
        // omitting it leaves large-world parse failures impossible to diagnose.
        textWriter.WriteLine(logEntry.State?.ToString());
        if (logEntry.Exception != null)
        {
            textWriter.WriteLine(logEntry.Exception);
        }
    }
}
