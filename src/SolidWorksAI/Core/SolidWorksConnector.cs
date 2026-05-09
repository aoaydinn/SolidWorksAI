using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksAI.Core;

/// <summary>
/// Çalışan SolidWorks 2026 örneğine COM üzerinden bağlanır.
/// Tüm COM çağrıları ayrı bir STA thread'inden yapılır.
/// </summary>
public class SolidWorksConnector : IDisposable
{
    // .NET 6+ sonrası Marshal.GetActiveObject kaldırıldı; P/Invoke ile erişiyoruz
    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(
        ref Guid rclsid,
        IntPtr pvReserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

    private ISldWorks? _swApp;
    private readonly Thread _staThread;
    private readonly BlockingCollection<Action> _staQueue = new();
    private bool _disposed;

    public bool IsConnected => _swApp != null;

    public SolidWorksConnector()
    {
        _staThread = new Thread(() =>
        {
            foreach (var action in _staQueue.GetConsumingEnumerable())
            {
                try { action(); }
                catch { /* bireysel hata TCS üzerinden iletilir */ }
            }
        });
        _staThread.SetApartmentState(ApartmentState.STA);
        _staThread.IsBackground = true;
        _staThread.Name = "SolidWorks-STA";
        _staThread.Start();
    }

    public Task<bool> ConnectAsync()
    {
        return RunOnStaAsync(() =>
        {
            try
            {
                if (_swApp != null)
                {
                    try { _swApp.RevisionNumber(); return true; }
                    catch { _swApp = null; }
                }

                var type = Type.GetTypeFromProgID("SldWorks.Application")
                    ?? throw new InvalidOperationException("SldWorks.Application ProgID bulunamadı.");
                var clsid = type.GUID;
                GetActiveObject(ref clsid, IntPtr.Zero, out var obj);
                _swApp = (ISldWorks)obj;
                _swApp.Visible = true;
                return true;
            }
            catch
            {
                _swApp = null;
                return false;
            }
        });
    }

    public void CheckConnection()
    {
        if (_swApp == null) return;
        try { _swApp.RevisionNumber(); }
        catch { _swApp = null; }
    }

    public void Disconnect()
    {
        if (_swApp != null)
        {
            Marshal.ReleaseComObject(_swApp);
            _swApp = null;
        }
    }

    public Task<T> RunOnStaAsync<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _staQueue.Add(() =>
        {
            try { tcs.SetResult(func()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    public Task RunOnStaAsync(Action action) =>
        RunOnStaAsync<bool>(() => { action(); return true; });

    /// <summary>
    /// COM RPC_E_SERVERCALL_RETRYLATER (0x8001010A) hatalarında üstel geri çekilme ile yeniden dener.
    /// SolidWorks meşgul olduğunda otomatik olarak tekrar denenir.
    /// </summary>
    public async Task<T> RunOnStaWithRetryAsync<T>(Func<T> func, int maxRetries = 3)
    {
        const int RPC_E_SERVERCALL_RETRYLATER = unchecked((int)0x8001010A);
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return await RunOnStaAsync(func).ConfigureAwait(false);
            }
            catch (COMException ex) when (ex.HResult == RPC_E_SERVERCALL_RETRYLATER)
            {
                if (attempt == maxRetries - 1) throw;
                // Üstel geri çekilme: 150ms → 300ms → 600ms
                await Task.Delay(150 * (1 << attempt)).ConfigureAwait(false);
            }
        }
        throw new InvalidOperationException("RunOnStaWithRetryAsync: Ulaşılamaz satır.");
    }

    public Task RunOnStaWithRetryAsync(Action action, int maxRetries = 3) =>
        RunOnStaWithRetryAsync<bool>(() => { action(); return true; }, maxRetries);

    public ISldWorks? SwApp => _swApp;

    public Task<IModelDoc2?> GetActiveDocAsync() =>
        RunOnStaAsync<IModelDoc2?>(() => _swApp?.ActiveDoc as IModelDoc2);

    public Task<IDrawingDoc?> GetActiveDrawingAsync() =>
        RunOnStaAsync<IDrawingDoc?>(() =>
        {
            if (_swApp?.ActiveDoc is IModelDoc2 doc &&
                doc.GetType() == (int)swDocumentTypes_e.swDocDRAWING)
                return doc as IDrawingDoc;
            return null;
        });

    public Task<string> GetSwVersionAsync() =>
        RunOnStaAsync(() => _swApp?.RevisionNumber() ?? "unknown");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _staQueue.CompleteAdding();
        Disconnect();
    }
}
