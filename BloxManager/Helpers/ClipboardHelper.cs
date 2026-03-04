using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace BloxManager.Helpers
{
    public static class ClipboardHelper
    {
        /// <summary>
        /// Sets text to the clipboard with retry logic and ensures execution on the UI thread.
        /// </summary>
        /// <param name="text">The text to copy.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task SetTextAsync(string text)
        {
            const int maxAttempts = 10;
            const int delayMs = 100;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    // Ensure we are on the UI thread
                    if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() => Clipboard.SetText(text));
                    }
                    else
                    {
                        Clipboard.SetText(text);
                    }
                    return; // success
                }
                catch (Exception) when (attempt < maxAttempts)
                {
                    // Clipboard may be busy; wait and retry
                    await Task.Delay(delayMs);
                }
            }
            // If we get here, all attempts failed
            throw new InvalidOperationException("Failed to set clipboard text after multiple attempts.");
        }
    }
}
