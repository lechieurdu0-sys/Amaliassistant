using System;
using System.IO;
using System.Threading;
using GameOverlay.Kikimeter.Core;

namespace GameOverlay.Kikimeter.Services;

public class LogFileWatcher : IDisposable
{
    private const string LogCategory = "LogFileWatcher";
    private const int PollingIntervalMs = 300;
    private readonly Core.LogParser _logParser = new();
    private readonly object _lifecycle = new();
    private Session? _session;
    private int _reading;

    private sealed class Session
    {
        public Session(string path) { Path = path; }
        public string Path { get; }
        public LogTailCursor Cursor { get; } = new();
        public System.Threading.Timer? Timer { get; set; }
        public DateTime? CreationTimeUtc { get; set; }
        public bool MissingAfterOpen { get; set; }
        public bool SkipHistoryOnFirstOpen { get; set; }
    }

    public Core.LogParser Parser => _logParser;
    public event EventHandler<string>? LogLineProcessed;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler? LogFileNotFound;

    public void StartWatching(string logPath)
    {
        StopWatching();
        if (string.IsNullOrWhiteSpace(logPath))
        {
            LogFileNotFound?.Invoke(this, EventArgs.Empty);
            return;
        }
        lock (_lifecycle)
        {
            var session = new Session(logPath);
            // Skip historical events only when the file exists at initial attachment.
            session.SkipHistoryOnFirstOpen = File.Exists(logPath);
            if (session.SkipHistoryOnFirstOpen)
            {
                try
                {
                    using var stream = Open(logPath);
                    session.CreationTimeUtc = File.GetCreationTimeUtc(logPath);
                    session.Cursor.InitializeAtEnd(stream);
                    session.SkipHistoryOnFirstOpen = false;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            Volatile.Write(ref _session, session);
            session.Timer = new System.Threading.Timer(_ => Poll(session), null, 0, PollingIntervalMs);
        }
        Logger.Info(LogCategory, "Surveillance séquentielle du journal démarrée.");
    }

    private static FileStream Open(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private bool IsCurrent(Session session) => ReferenceEquals(Volatile.Read(ref _session), session);

    private void Poll(Session session)
    {
        if (!IsCurrent(session) || Interlocked.Exchange(ref _reading, 1) != 0)
            return;
        try
        {
            if (!IsCurrent(session)) return;
            if (!File.Exists(session.Path))
            {
                if (session.CreationTimeUtc.HasValue) session.MissingAfterOpen = true;
                session.SkipHistoryOnFirstOpen = false;
                return;
            }
            using var stream = Open(session.Path);
            DateTime creation = File.GetCreationTimeUtc(session.Path);
            if (session.SkipHistoryOnFirstOpen)
            {
                session.Cursor.InitializeAtEnd(stream);
                session.SkipHistoryOnFirstOpen = false;
                session.CreationTimeUtc = creation;
                return;
            }
            bool replaced = session.MissingAfterOpen ||
                (session.CreationTimeUtc.HasValue && session.CreationTimeUtc.Value != creation);
            session.CreationTimeUtc = creation;
            session.MissingAfterOpen = false;
            session.Cursor.Read(stream, line =>
            {
                if (!IsCurrent(session)) return;
                _logParser.ProcessLogLine(line);
                if (!IsCurrent(session)) return;
                try { LogLineProcessed?.Invoke(this, line); }
                catch (Exception ex) { Logger.Warning(LogCategory, "Erreur destinataire du log: " + ex.Message); }
            }, () =>
            {
                if (IsCurrent(session)) _logParser.Reset();
            }, replaced);
        }
        catch (IOException) { /* Writer may temporarily lock or replace the file. */ }
        catch (UnauthorizedAccessException) { /* Retry on the next tick. */ }
        catch (Exception ex)
        {
            Logger.Error(LogCategory, "Erreur de lecture: " + ex.Message);
            if (IsCurrent(session)) ErrorOccurred?.Invoke(this, ex.Message);
        }
        finally { Volatile.Write(ref _reading, 0); }
    }

    public void ManualRead()
    {
        var session = Volatile.Read(ref _session);
        if (session != null) System.Threading.Tasks.Task.Run(() => Poll(session));
    }

    public void StopWatching()
    {
        lock (_lifecycle)
        {
            var session = Interlocked.Exchange(ref _session, null);
            session?.Timer?.Dispose();
        }
    }

    public void Dispose() => StopWatching();
}
