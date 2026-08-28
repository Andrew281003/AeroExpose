namespace AeroExpose.UI;

public sealed class WindowSelectionRequestedEventArgs : EventArgs
{
    public WindowSelectionRequestedEventArgs(nint windowHandle)
    {
        WindowHandle = windowHandle;
    }

    public nint WindowHandle { get; }
}
