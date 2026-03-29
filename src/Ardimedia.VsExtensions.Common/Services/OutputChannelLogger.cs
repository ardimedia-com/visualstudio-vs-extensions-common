namespace Ardimedia.VsExtensions.Common.Services;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Documents;

/// <summary>
/// Writes log messages to a dedicated VS Output Window channel.
/// Creates the channel lazily on first write. Fire-and-forget pattern.
/// </summary>
public class OutputChannelLogger : IDisposable
{
    private readonly VisualStudioExtensibility _extensibility;
    private readonly string _channelName;
    private OutputChannel? _outputChannel;

    public OutputChannelLogger(VisualStudioExtensibility extensibility, string channelName)
    {
        _extensibility = extensibility;
        _channelName = channelName;
    }

    /// <summary>
    /// Writes a line to the VS Output Window channel. Fire-and-forget.
    /// </summary>
    public void WriteLine(string message)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                _outputChannel ??= await _extensibility.Views().Output
                    .CreateOutputChannelAsync(_channelName, default);

                await _outputChannel.Writer.WriteLineAsync(message);
            }
            catch
            {
                // Output channel not available -- silently ignore
            }
        });
    }

    public void Dispose()
    {
        _outputChannel?.Dispose();
        GC.SuppressFinalize(this);
    }
}
