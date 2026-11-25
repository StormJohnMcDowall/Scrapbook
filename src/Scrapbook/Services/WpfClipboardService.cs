using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Scrapbook.Core;

namespace Scrapbook.Services;

public sealed class WpfClipboardService : IClipboardService
{
    // Registered clipboard formats Windows checks before adding content to clipboard history
    // (Win+V) or syncing it to the cloud clipboard.
    private const string ExcludeFromMonitoring = "ExcludeClipboardContentFromMonitorProcessing";
    private const string CanIncludeInHistory = "CanIncludeInClipboardHistory";
    private const string CanUploadToCloud = "CanUploadToCloudClipboard";

    public bool TrySetText(string text, bool sensitive)
    {
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, text);

        if (sensitive)
        {
            data.SetData(ExcludeFromMonitoring, new MemoryStream(new byte[4]));
            data.SetData(CanIncludeInHistory, new MemoryStream(BitConverter.GetBytes(0)));
            data.SetData(CanUploadToCloud, new MemoryStream(BitConverter.GetBytes(0)));
        }

        // Another app can briefly hold the clipboard open; retry for up to ~0.5s.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(data, copy: true);
                return true;
            }
            catch (ExternalException) when (attempt < MaxAttempts)
            {
                Thread.Sleep(RetryDelayMs);
            }
            catch (ExternalException)
            {
                return false;
            }
        }
    }

    private const int MaxAttempts = 10;
    private const int RetryDelayMs = 50;
}
