namespace Ardimedia.VsExtensions.Common.ViewModels;

using System.Runtime.Serialization;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.UI;
using Microsoft.VisualStudio.ProjectSystem.Query;

/// <summary>
/// Base ViewModel for VS Extensibility tool windows with common infrastructure:
/// - Solution monitoring with two-pass debounce
/// - IsScanning state with Analyse/Cancel button visibility
/// - CancellationTokenSource management
/// - Solution fingerprint tracking
///
/// Subclasses override <see cref="OnSolutionOpenedAsync"/> and <see cref="OnSolutionClosed"/>
/// to react to solution changes.
/// </summary>
[DataContract]
public abstract class ToolWindowViewModelBase : NotifyPropertyChangedObject, IDisposable
{
    protected readonly VisualStudioExtensibility Extensibility;

    private bool _isScanning;
    private string _lastSolutionFingerprint = string.Empty;
    private CancellationTokenSource? _monitorCts;
    private CancellationTokenSource? _scanCts;

    protected ToolWindowViewModelBase(VisualStudioExtensibility extensibility)
    {
        this.Extensibility = extensibility;
        this.StartAnalyseCommand = new AsyncCommand(this.ExecuteStartAnalyseAsync);
        this.CancelAnalyseCommand = new AsyncCommand(this.ExecuteCancelAsync);
    }

    #region Properties

    [DataMember]
    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (this.SetProperty(ref _isScanning, value))
            {
                this.RaiseNotifyPropertyChangedEvent(nameof(this.AnalyseButtonVisibility));
                this.RaiseNotifyPropertyChangedEvent(nameof(this.CancelButtonVisibility));
                this.OnIsScanningChanged();
            }
        }
    }

    /// <summary>Analyse button visible when NOT scanning.</summary>
    [DataMember]
    public string AnalyseButtonVisibility => _isScanning ? "Collapsed" : "Visible";

    /// <summary>Cancel button visible when scanning.</summary>
    [DataMember]
    public string CancelButtonVisibility => _isScanning ? "Visible" : "Collapsed";

    #endregion

    #region Commands

    [DataMember]
    public IAsyncCommand StartAnalyseCommand { get; }

    [DataMember]
    public IAsyncCommand CancelAnalyseCommand { get; }

    #endregion

    #region Public Methods

    /// <summary>
    /// Call from ToolWindow.GetContentAsync to start monitoring and initial scan.
    /// </summary>
    public void Initialize()
    {
        this.StartSolutionMonitor();
        _ = Task.Run(async () =>
        {
            // Small delay to let VS finish loading
            await Task.Delay(500);
            await this.TryStartScanAsync();
        });
    }

    public virtual void Dispose()
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Protected Methods (override in subclass)

    /// <summary>
    /// Called when a solution is opened or changed. Subclass should run its analysis here.
    /// </summary>
    protected abstract Task OnSolutionOpenedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Called when a solution is closed. Subclass should clear its data.
    /// </summary>
    protected abstract void OnSolutionClosed();

    /// <summary>
    /// Called when IsScanning changes. Subclass can raise additional property notifications.
    /// </summary>
    protected virtual void OnIsScanningChanged() { }

    /// <summary>
    /// Provides the CancellationToken for the current scan. Use in subclass scan methods.
    /// </summary>
    protected CancellationToken ScanCancellationToken => _scanCts?.Token ?? CancellationToken.None;

    #endregion

    #region Private Methods

    private Task ExecuteStartAnalyseAsync(object? parameter, CancellationToken cancellationToken)
    {
        if (_isScanning)
        {
            return Task.CompletedTask;
        }

        return this.TryStartScanAsync();
    }

    private Task ExecuteCancelAsync(object? parameter, CancellationToken cancellationToken)
    {
        _scanCts?.Cancel();
        return Task.CompletedTask;
    }

    private Task TryStartScanAsync()
    {
        if (_isScanning)
        {
            return Task.CompletedTask;
        }

        this.IsScanning = true;

        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await this.OnSolutionOpenedAsync(token);

                _lastSolutionFingerprint = await this.GetSolutionFingerprintAsync(token);
            }
            catch (OperationCanceledException)
            {
                // Expected when user cancels
            }
            finally
            {
                this.IsScanning = false;
            }
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Background monitor that detects solution changes (open/close/switch).
    /// Uses two-pass debounce: fingerprint must be stable for TWO consecutive
    /// checks (5 seconds apart) to ensure all projects are fully loaded.
    /// </summary>
    protected virtual void StartSolutionMonitor()
    {
        _monitorCts?.Cancel();
        _monitorCts = new CancellationTokenSource();
        var token = _monitorCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);

                    if (_isScanning)
                    {
                        continue;
                    }

                    var currentFingerprint = await this.GetSolutionFingerprintAsync(token).ConfigureAwait(false);

                    // Solution was closed
                    if (string.IsNullOrEmpty(currentFingerprint) &&
                        !string.IsNullOrEmpty(_lastSolutionFingerprint))
                    {
                        _lastSolutionFingerprint = string.Empty;
                        this.OnSolutionClosed();
                        continue;
                    }

                    // Solution opened or changed -- two-pass debounce
                    if (!string.IsNullOrEmpty(currentFingerprint) &&
                        currentFingerprint != _lastSolutionFingerprint)
                    {
                        int stableCount = 0;
                        string lastFingerprint = "";

                        while (stableCount < 2 && !token.IsCancellationRequested)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
                            currentFingerprint = await this.GetSolutionFingerprintAsync(token).ConfigureAwait(false);

                            if (string.IsNullOrEmpty(currentFingerprint))
                            {
                                break;
                            }

                            if (currentFingerprint == lastFingerprint)
                            {
                                stableCount++;
                            }
                            else
                            {
                                stableCount = 0;
                                lastFingerprint = currentFingerprint;
                            }
                        }

                        if (!string.IsNullOrEmpty(currentFingerprint) && stableCount >= 2)
                        {
                            await this.TryStartScanAsync();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    // Monitor should never crash
                }
            }
        }, token);
    }

    /// <summary>
    /// Returns a fingerprint of the current solution (sorted project paths joined).
    /// Returns empty if no solution is loaded.
    /// </summary>
    protected async Task<string> GetSolutionFingerprintAsync(CancellationToken cancellationToken)
    {
        var projects = await this.Extensibility.Workspaces().QueryProjectsAsync(
            q => q.With(p => p.Path),
            cancellationToken).ConfigureAwait(false);

        var paths = projects
            .Select(p => p.Path ?? string.Empty)
            .Where(p => !string.IsNullOrEmpty(p))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (paths.Count == 0) return string.Empty;
        return string.Join("|", paths);
    }

    #endregion
}
