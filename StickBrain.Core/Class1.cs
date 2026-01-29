using System.Text;

namespace StickBrain.Core;

/// <summary>
/// 노트 도메인 모델 (DB의 NoteEntity와 1:1 매핑)
/// </summary>
public sealed class Note
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Text { get; set; } = string.Empty;
    public long CreatedAt { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public int Color { get; set; } = unchecked((int)0xFFFFF59D); // 기본 포스트잇 색 (연한 노랑, ARGB)
    public double X { get; set; } = 0;
    public double Y { get; set; } = 0;
    public bool IsPinned { get; set; }
    public string? AppName { get; set; }
    public string? WindowTitle { get; set; }
}

/// <summary>
/// 검색 결과 점수 및 메타 정보
/// </summary>
public sealed record NoteSearchResult(Note Note, double Score);

/// <summary>
/// 노트 저장/조회용 추상 리포지토리
/// </summary>
public interface INoteRepository
{
    Task AddAsync(Note note, CancellationToken cancellationToken = default);
    Task UpdateAsync(Note note, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<Note?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Note>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NoteSearchResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default);
}

/// <summary>
/// 검색 및 스택(클러스터링) 결과 모델
/// </summary>
public sealed class NoteStack
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<NoteSearchResult> Items { get; init; } = Array.Empty<NoteSearchResult>();
}

/// <summary>
/// 검색 및 스택 그룹핑 전략 인터페이스
/// </summary>
public interface INoteStackGrouper
{
    IReadOnlyList<NoteStack> GroupIntoStacks(IReadOnlyList<NoteSearchResult> results);
}

/// <summary>
/// 간단한 토큰 기반 + 시간 가중치 클러스터링 구현체 (MVP)
/// </summary>
public sealed class SimpleTokenBasedNoteStackGrouper : INoteStackGrouper
{
    public IReadOnlyList<NoteStack> GroupIntoStacks(IReadOnlyList<NoteSearchResult> results)
    {
        // TODO: 실제 토큰 기반 greedy clustering 구현
        // 현재는 MVP로 전체를 하나의 스택으로 반환한다.
        if (results.Count == 0)
        {
            return Array.Empty<NoteStack>();
        }

        var title = BuildTitleFromTokens(results.Select(r => r.Note.Text));

        return new[]
        {
            new NoteStack
            {
                Title = title,
                Items = results
            }
        };
    }

    private static string BuildTitleFromTokens(IEnumerable<string> texts)
    {
        // 매우 단순한 토큰 빈도 기반 제목 (상위 1~2개 토큰)
        var freq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var text in texts)
        {
            foreach (var token in Tokenize(text))
            {
                if (token.Length < 2) continue;
                freq[token] = freq.GetValueOrDefault(token) + 1;
            }
        }

        var top = freq.OrderByDescending(kv => kv.Value)
                      .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                      .Take(2)
                      .Select(kv => kv.Key)
                      .ToArray();

        return top.Length == 0 ? "Notes" : string.Join(" / ", top);
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;

        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                if (sb.Length > 0)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }
            }
        }

        if (sb.Length > 0)
        {
            yield return sb.ToString();
        }
    }
}
