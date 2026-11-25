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

        // WPF already retries internally (~1s) when another app briefly holds the clipboard open.
        // Don't add a retry loop on top: it would freeze the window mid-demo.
        try
        {
            Clipboard.SetDataObject(data, copy: true);
            return true;
        }
        catch (ExternalException)
        {
            return false;
        }
    }
}
