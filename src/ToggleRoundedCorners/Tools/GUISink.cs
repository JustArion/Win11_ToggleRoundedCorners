using Serilog.Events;

namespace Dawn.Apps.ToggleRoundedCorners.Tools;

public class GUISink(ILogger logger) :ILogger
{
    public delegate void LogHandler(LogEventLevel logLevel, string message);
    public LogHandler? OnLog { get; set; }

    public void Write(LogEvent logEvent)
    {
        logger.Write(logEvent);

        if (OnLog == null)
            return;
        
        try
        {
            OnLog(logEvent.Level,
                logEvent.Exception != null
                    ? $"{logEvent.RenderMessage()} {logEvent.Exception.Message}"
                    : logEvent.RenderMessage());
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Exception within GUI Sink");
        }
    }
}