namespace AeroExpose.Input;

/// <summary>Implemented by hotkeys today and by future gesture adapters.</summary>
internal interface IOverviewTrigger : IDisposable
{
    event EventHandler? Triggered;
}
