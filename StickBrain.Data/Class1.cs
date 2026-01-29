using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using StickBrain.Core;

namespace StickBrain.Data;

/// <summary>
/// SQLite 기반 Note 리포지토리 구현
/// </summary>
public sealed class SqliteNoteRepository : INoteRepository, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteNoteRepository> _logger;

    public SqliteNoteRepository(string databasePath, ILogger<SqliteNoteRepository> logger)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Cache = SqliteCacheMode.Shared,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        _logger = logger;

        InitializeAsync().GetAwaiter().GetResult();
    }

    private async Task InitializeAsync()
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var command = connection.CreateCommand();
            command.CommandText =
                """
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;

                CREATE TABLE IF NOT EXISTS notes (
                    id TEXT PRIMARY KEY,
                    text TEXT NOT NULL,
                    created_at INTEGER NOT NULL,
                    updated_at INTEGER NOT NULL,
                    color INTEGER NOT NULL,
                    x REAL NOT NULL,
                    y REAL NOT NULL,
                    is_pinned INTEGER NOT NULL,
                    app_name TEXT NULL,
                    window_title TEXT NULL
                );

                CREATE VIRTUAL TABLE IF NOT EXISTS notes_fts USING fts5(
                    text,
                    content='notes',
                    content_rowid='rowid'
                );

                CREATE TRIGGER IF NOT EXISTS notes_ai AFTER INSERT ON notes BEGIN
                    INSERT INTO notes_fts(rowid, text) VALUES (new.rowid, new.text);
                END;

                CREATE TRIGGER IF NOT EXISTS notes_ad AFTER DELETE ON notes BEGIN
                    INSERT INTO notes_fts(notes_fts, rowid, text) VALUES('delete', old.rowid, old.text);
                END;

                CREATE TRIGGER IF NOT EXISTS notes_au AFTER UPDATE ON notes BEGIN
                    INSERT INTO notes_fts(notes_fts, rowid, text) VALUES('delete', old.rowid, old.text);
                    INSERT INTO notes_fts(rowid, text) VALUES (new.rowid, new.text);
                END;
                """;

            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // DB/FTS 실패 시에도 앱이 죽지 않도록 로그만 남기고 계속 진행
            _logger.LogError(ex, "SQLite 초기화 중 오류 발생");
        }
    }

    private SqliteConnection CreateConnection() => new(_connectionString);

    public async Task AddAsync(Note note, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO notes (id, text, created_at, updated_at, color, x, y, is_pinned, app_name, window_title)
                VALUES ($id, $text, $created_at, $updated_at, $color, $x, $y, $is_pinned, $app_name, $window_title);
                """;

            BindNoteParameters(cmd, note);

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "노트 추가 중 오류 발생");
            // 실패해도 상위 앱이 죽지 않도록 예외 전파를 막는다.
        }
    }

    public async Task UpdateAsync(Note note, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                UPDATE notes
                SET text = $text,
                    created_at = $created_at,
                    updated_at = $updated_at,
                    color = $color,
                    x = $x,
                    y = $y,
                    is_pinned = $is_pinned,
                    app_name = $app_name,
                    window_title = $window_title
                WHERE id = $id;
                """;

            BindNoteParameters(cmd, note);

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "노트 업데이트 중 오류 발생");
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM notes WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "노트 삭제 중 오류 발생");
        }
    }

    public async Task<Note?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, text, created_at, updated_at, color, x, y, is_pinned, app_name, window_title FROM notes WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return MapNote(reader);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "노트 조회 중 오류 발생");
        }

        return null;
    }

    public async Task<IReadOnlyList<Note>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<Note>();
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, text, created_at, updated_at, color, x, y, is_pinned, app_name, window_title FROM notes;";

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add(MapNote(reader));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "전체 노트 조회 중 오류 발생");
        }

        return list;
    }

    public async Task<IReadOnlyList<NoteSearchResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default)
    {
        var results = new List<NoteSearchResult>();
        if (string.IsNullOrWhiteSpace(query))
        {
            return results;
        }

        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // 1차: FTS5로 후보 검색
            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT n.id,
                       n.text,
                       n.created_at,
                       n.updated_at,
                       n.color,
                       n.x,
                       n.y,
                       n.is_pinned,
                       n.app_name,
                       n.window_title,
                       bm25(notes_fts) AS rank
                FROM notes_fts
                JOIN notes n ON n.rowid = notes_fts.rowid
                WHERE notes_fts MATCH $query
                ORDER BY rank
                LIMIT $limit;
                """;

            cmd.Parameters.AddWithValue("$query", query);
            cmd.Parameters.AddWithValue("$limit", limit);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var note = MapNote(reader);
                var rank = reader.GetDouble(reader.GetOrdinal("rank"));

                // 2차: 간단한 시간 가중치 추가 (최근일수록 점수 보정)
                var ageSeconds = Math.Max(1, DateTimeOffset.UtcNow.ToUnixTimeSeconds() - note.UpdatedAt);
                var recencyBoost = 1.0 + (1.0 / Math.Sqrt(ageSeconds));
                var score = -rank * recencyBoost;

                results.Add(new NoteSearchResult(note, score));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "노트 검색 중 오류 발생");
        }

        return results
            .OrderByDescending(r => r.Score)
            .ToList();
    }

    private static void BindNoteParameters(SqliteCommand cmd, Note note)
    {
        cmd.Parameters.AddWithValue("$id", note.Id);
        cmd.Parameters.AddWithValue("$text", note.Text);
        cmd.Parameters.AddWithValue("$created_at", note.CreatedAt);
        cmd.Parameters.AddWithValue("$updated_at", note.UpdatedAt);
        cmd.Parameters.AddWithValue("$color", note.Color);
        cmd.Parameters.AddWithValue("$x", note.X);
        cmd.Parameters.AddWithValue("$y", note.Y);
        cmd.Parameters.AddWithValue("$is_pinned", note.IsPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("$app_name", (object?)note.AppName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$window_title", (object?)note.WindowTitle ?? DBNull.Value);
    }

    private static Note MapNote(SqliteDataReader reader)
    {
        return new Note
        {
            Id = reader.GetString(0),
            Text = reader.GetString(1),
            CreatedAt = reader.GetInt64(2),
            UpdatedAt = reader.GetInt64(3),
            Color = reader.GetInt32(4),
            X = reader.GetDouble(5),
            Y = reader.GetDouble(6),
            IsPinned = reader.GetInt32(7) != 0,
            AppName = reader.IsDBNull(8) ? null : reader.GetString(8),
            WindowTitle = reader.IsDBNull(9) ? null : reader.GetString(9)
        };
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
