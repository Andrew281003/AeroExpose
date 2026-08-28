using AeroExpose.Core.Models;

namespace AeroExpose.UI;

public interface IMissionControlOverlay : IDisposable
{
    bool IsOverviewVisible { get; }

    event EventHandler? DismissRequested;

    event EventHandler<WindowSelectionRequestedEventArgs>? WindowSelectionRequested;

    Task ShowOverviewAsync(MissionControlSession session, CancellationToken cancellationToken);

    Task HideOverviewAsync(nint? selectedWindow, CancellationToken cancellationToken);
}
