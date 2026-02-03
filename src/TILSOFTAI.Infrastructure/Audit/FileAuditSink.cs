using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Audit;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Audit;

/// <summary>
/// Writes audit events to JSON Lines files with auto-rotation.
/// </summary>
public sealed class FileAuditSink : IAuditSink, IDisposable
{
    private readonly AuditOptions _options;
    private readonly ILogger<FileAuditSink> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;

    private StreamWriter? _currentWriter;
    private string? _currentFilePath;
    private long _currentFileSize;
    private DateOnly _currentFileDate;

    public string Name => "File";
    public bool IsEnabled => _options.FileEnabled;

    public FileAuditSink(
        IOptions<AuditOptions> options,
        ILogger<FileAuditSink> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        EnsureDirectoryExists();
    }

    public async Task WriteBatchAsync(IReadOnlyList<AuditEvent> events, CancellationToken ct)
    {
        if (events.Count == 0) return;

        await _writeLock.WaitAsync(ct);
        try
        {
            await EnsureWriterAsync();

            foreach (var evt in events)
            {
                var json = JsonSerializer.Serialize(evt, evt.GetType(), _jsonOptions);
                await _currentWriter!.WriteLineAsync(json);
                _currentFileSize += json.Length + Environment.NewLine.Length;

                // Check for rotation
                if (_currentFileSize >= _options.MaxFileSizeBytes || DateOnly.FromDateTime(DateTime.UtcNow) != _currentFileDate)
                {
                    await RotateFileAsync();
                }
            }

            await _currentWriter!.FlushAsync(ct);

            _logger.LogDebug("Wrote {Count} audit events to file {Path}", events.Count, _currentFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write {Count} audit events to file", events.Count);
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_options.FilePath))
        {
            Directory.CreateDirectory(_options.FilePath);
        }
    }

    private async Task EnsureWriterAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (_currentWriter is not null && _currentFileDate == today)
        {
            return;
        }

        await RotateFileAsync();
    }

    private async Task RotateFileAsync()
    {
        if (_currentWriter is not null)
        {
            await _currentWriter.FlushAsync();
            await _currentWriter.DisposeAsync();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fileName = $"audit-{today:yyyy-MM-dd}";
        var counter = 0;

        // Find next available file name
        do
        {
            var suffix = counter == 0 ? "" : $"-{counter}";
            _currentFilePath = Path.Combine(_options.FilePath, $"{fileName}{suffix}.jsonl");
            counter++;
        }
        while (File.Exists(_currentFilePath) && new FileInfo(_currentFilePath).Length >= _options.MaxFileSizeBytes);

        _currentWriter = new StreamWriter(_currentFilePath, append: true);
        _currentFileSize = File.Exists(_currentFilePath) ? new FileInfo(_currentFilePath).Length : 0;
        _currentFileDate = today;

        _logger.LogInformation("Audit file rotated to {Path}", _currentFilePath);
    }

    public void Dispose()
    {
        _writeLock.Wait();
        try
        {
            _currentWriter?.Flush();
            _currentWriter?.Dispose();
        }
        finally
        {
            _writeLock.Release();
            _writeLock.Dispose();
        }
    }
}
