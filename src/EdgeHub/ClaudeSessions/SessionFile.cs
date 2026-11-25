using System.Buffers;
using System.Globalization;
using System.Text.Json;

namespace EdgeHub.ClaudeSessions;

/// <summary>
/// The metadata of one Claude Code session file (or of one of its subagents), read a little more at each call: the
/// file only grows, so only the complete lines added since the last call are read. A line still being written waits
/// for its line break. Only an allowlist of fields is read; the conversation itself is never kept.
/// </summary>
internal sealed class SessionFile(string path)
{
    private long _offset;

    public string Path { get; } = path;

    /// <summary>The first timestamp of the file: when the session started.</summary>
    public DateTimeOffset? FirstTimestamp { get; private set; }

    /// <summary>The timestamp of the last <c>user</c> or <c>assistant</c> line: the last sign of work.</summary>
    public DateTimeOffset? LastActivity { get; private set; }

    /// <summary><c>cwd</c> of the first message: the project, whatever folder the session moved to later.</summary>
    public string? FirstCwd { get; private set; }

    /// <summary><c>gitBranch</c> of the last message.</summary>
    public string? LastBranch { get; private set; }

    /// <summary>The last <c>/rename</c>, which wins over generated titles whatever their order.</summary>
    public string? CustomTitle { get; private set; }

    public string? AiTitle { get; private set; }

    /// <summary>Files edited successfully through Edit or Write (<c>toolUseResult.filePath</c>).</summary>
    public HashSet<string> EditedFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>When the reader last needed this file, to forget files of sessions long finished.</summary>
    public DateTimeOffset LastNeeded { get; set; }

    public void ReadNewLines()
    {
        using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.SequentialScan);
        if (stream.Length < _offset)
        {
            // Replaced by a shorter file: start over.
            Reset();
        }

        stream.Seek(_offset, SeekOrigin.Begin);
        byte[] chunk = ArrayPool<byte>.Shared.Rent(64 * 1024);
        var unfinished = new ArrayBufferWriter<byte>();
        try
        {
            int read;
            while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                var rest = new ReadOnlySpan<byte>(chunk, 0, read);
                int end;
                while ((end = rest.IndexOf((byte)'\n')) >= 0)
                {
                    if (unfinished.WrittenCount > 0)
                    {
                        unfinished.Write(rest[..end]);
                        ReadLine(unfinished.WrittenSpan);
                        _offset += unfinished.WrittenCount + 1;
                        unfinished.Clear();
                    }
                    else
                    {
                        ReadLine(rest[..end]);
                        _offset += end + 1;
                    }

                    rest = rest[(end + 1)..];
                }

                unfinished.Write(rest);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }
    }

    private void Reset()
    {
        _offset = 0;
        FirstTimestamp = LastActivity = null;
        FirstCwd = LastBranch = CustomTitle = AiTitle = null;
        EditedFiles.Clear();
    }

    private void ReadLine(ReadOnlySpan<byte> line)
    {
        TopLevel top;
        try
        {
            top = TopLevel.Read(line);
        }
        catch (JsonException)
        {
            // A broken line is skipped.
            return;
        }

        DateTimeOffset? at = top.Timestamp is { } stamp
            && DateTimeOffset.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed)
            ? parsed
            : null;
        FirstTimestamp ??= at;

        switch (top.Type)
        {
            case "custom-title" when !string.IsNullOrWhiteSpace(top.CustomTitle):
                CustomTitle = top.CustomTitle;
                break;
            case "ai-title" when !string.IsNullOrWhiteSpace(top.AiTitle):
                AiTitle = top.AiTitle;
                break;
            case "user" or "assistant":
                LastActivity = at ?? LastActivity;
                FirstCwd ??= top.Cwd;
                LastBranch = top.GitBranch ?? LastBranch;
                if (top.Type == "user" && top.HasToolUseResult && EditedFile(line) is { } file)
                {
                    EditedFiles.Add(file);
                }

                break;
            default:
                // The many other types are of no use here.
                break;
        }
    }

    /// <summary>
    /// The file of a successful Edit or Write. Only these lines are parsed in full, and only
    /// <c>toolUseResult.filePath</c> and the error flag of the tool result are looked at.
    /// </summary>
    private static string? EditedFile(ReadOnlySpan<byte> line)
    {
        using JsonDocument document = JsonDocument.Parse(line.ToArray());
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("toolUseResult", out JsonElement result)
            || result.ValueKind != JsonValueKind.Object
            || !result.TryGetProperty("filePath", out JsonElement file)
            || file.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        bool failed = root.TryGetProperty("message", out JsonElement message)
            && message.ValueKind == JsonValueKind.Object
            && message.TryGetProperty("content", out JsonElement content)
            && content.ValueKind == JsonValueKind.Array
            && content.EnumerateArray().Any(block =>
                block.ValueKind == JsonValueKind.Object
                && block.TryGetProperty("is_error", out JsonElement error)
                && error.ValueKind == JsonValueKind.True);
        return failed ? null : file.GetString();
    }

    /// <summary>
    /// The top-level fields Edge Hub reads, without building the whole document: lines can weigh more than a megabyte.
    /// </summary>
    private readonly record struct TopLevel(
        string? Type,
        string? Timestamp,
        string? Cwd,
        string? GitBranch,
        string? CustomTitle,
        string? AiTitle,
        bool HasToolUseResult)
    {
        public static TopLevel Read(ReadOnlySpan<byte> line)
        {
            var reader = new Utf8JsonReader(line);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return default;
            }

            var top = default(TopLevel);
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                string? name = Known(ref reader);
                reader.Read();
                if (name == "toolUseResult")
                {
                    top = top with { HasToolUseResult = true };
                }

                if (name is null || name == "toolUseResult" || reader.TokenType != JsonTokenType.String)
                {
                    reader.Skip();
                    continue;
                }

                string? value = reader.GetString();
                top = name switch
                {
                    "type" => top with { Type = value },
                    "timestamp" => top with { Timestamp = value },
                    "cwd" => top with { Cwd = value },
                    "gitBranch" => top with { GitBranch = value },
                    "customTitle" => top with { CustomTitle = value },
                    _ => top with { AiTitle = value },
                };
            }

            return top;
        }

        private static string? Known(ref Utf8JsonReader reader) =>
            reader.ValueTextEquals("type"u8) ? "type"
            : reader.ValueTextEquals("timestamp"u8) ? "timestamp"
            : reader.ValueTextEquals("cwd"u8) ? "cwd"
            : reader.ValueTextEquals("gitBranch"u8) ? "gitBranch"
            : reader.ValueTextEquals("customTitle"u8) ? "customTitle"
            : reader.ValueTextEquals("aiTitle"u8) ? "aiTitle"
            : reader.ValueTextEquals("toolUseResult"u8) ? "toolUseResult"
            : null;
    }
}
