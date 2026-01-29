using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using StickBrain.Core;

namespace StickBrain.App;

public sealed class StickyWallViewModel
{
    private readonly INoteRepository _repository;
    private readonly ILogger<StickyWallViewModel> _logger;

    public ObservableCollection<StickyNoteViewModel> Notes { get; } = new();

    public ICollectionView NotesView { get; }

    public enum TimeFilterMode
    {
        All,
        Today,
        Last7Days,
        Last30Days
    }

    public enum SortMode
    {
        UpdatedDesc,
        CreatedDesc
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value ?? string.Empty;
            NotesView.Refresh();
        }
    }

    private TimeFilterMode _timeFilter = TimeFilterMode.All;
    public TimeFilterMode TimeFilter
    {
        get => _timeFilter;
        set
        {
            if (_timeFilter == value) return;
            _timeFilter = value;
            NotesView.Refresh();
        }
    }

    private SortMode _sort = SortMode.UpdatedDesc;
    public SortMode Sort
    {
        get => _sort;
        set
        {
            if (_sort == value) return;
            _sort = value;
            ApplySort();
        }
    }

    public StickyWallViewModel(INoteRepository repository, ILogger<StickyWallViewModel> logger)
    {
        _repository = repository;
        _logger = logger;

        NotesView = CollectionViewSource.GetDefaultView(Notes);
        NotesView.Filter = FilterNote;
        ApplySort();

        _ = LoadAsync();
    }

    public void AddNote(Note note)
    {
        try
        {
            Notes.Add(CreateNoteViewModel(note));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StickyWall에 노트 추가 중 오류 발생");
        }
    }

    private bool FilterNote(object obj)
    {
        if (obj is not StickyNoteViewModel vm) return false;
        if (!MatchesSearch(vm)) return false;
        if (!MatchesTimeFilter(vm)) return false;
        return true;
    }

    private bool MatchesSearch(StickyNoteViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        var keyword = SearchText.Trim();
        return (vm.Text?.IndexOf(keyword, StringComparison.CurrentCultureIgnoreCase) ?? -1) >= 0;
    }

    private bool MatchesTimeFilter(StickyNoteViewModel vm)
    {
        if (TimeFilter == TimeFilterMode.All) return true;

        var updated = vm.UpdatedAt;
        var now = DateTimeOffset.Now;

        return TimeFilter switch
        {
            TimeFilterMode.Today => updated.Date == now.Date,
            TimeFilterMode.Last7Days => updated >= now.AddDays(-7),
            TimeFilterMode.Last30Days => updated >= now.AddDays(-30),
            _ => true
        };
    }

    private void ApplySort()
    {
        using (NotesView.DeferRefresh())
        {
            NotesView.SortDescriptions.Clear();

            var prop = Sort == SortMode.CreatedDesc ? nameof(StickyNoteViewModel.CreatedAt) : nameof(StickyNoteViewModel.UpdatedAt);
            NotesView.SortDescriptions.Add(new SortDescription(prop, ListSortDirection.Descending));
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            var notes = await _repository.GetAllAsync();
            foreach (var n in notes)
            {
                Notes.Add(CreateNoteViewModel(n));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StickyWall 노트 로드 중 오류 발생");
        }
    }

    private StickyNoteViewModel CreateNoteViewModel(Note note)
        => new(note, _repository, _logger, OnNoteDeleted);

    private void OnNoteDeleted(StickyNoteViewModel vm)
    {
        if (Notes.Contains(vm))
        {
            Notes.Remove(vm);
        }
    }
}

public sealed class StickyNoteViewModel : INotifyPropertyChanged
{
    private readonly INoteRepository _repository;
    private readonly ILogger _logger;
    private readonly Note _note;
    private readonly object _positionLock = new();
    private readonly object _textLock = new();
    private CancellationTokenSource? _positionDebounceCts;
    private CancellationTokenSource? _textDebounceCts;

    public string Id => _note.Id;

    public DateTimeOffset CreatedAt => DateTimeOffset.FromUnixTimeSeconds(_note.CreatedAt);

    public DateTimeOffset UpdatedAt => DateTimeOffset.FromUnixTimeSeconds(_note.UpdatedAt);

    public string Text
    {
        get => _note.Text;
        set
        {
            if (_note.Text == value) return;
            _note.Text = value;
            OnPropertyChanged();
        }
    }

    public double X
    {
        get => _note.X;
        set
        {
            if (Math.Abs(_note.X - value) < 0.5) return;
            _note.X = value;
            OnPropertyChanged();
        }
    }

    public double Y
    {
        get => _note.Y;
        set
        {
            if (Math.Abs(_note.Y - value) < 0.5) return;
            _note.Y = value;
            OnPropertyChanged();
        }
    }

    public Brush ColorBrush => new SolidColorBrush(Color.FromArgb(
        (byte)((_note.Color >> 24) & 0xFF),
        (byte)((_note.Color >> 16) & 0xFF),
        (byte)((_note.Color >> 8) & 0xFF),
        (byte)(_note.Color & 0xFF)));

    private readonly Action<StickyNoteViewModel> _onDeleted;

    public StickyNoteViewModel(Note note, INoteRepository repository, ILogger logger, Action<StickyNoteViewModel> onDeleted)
    {
        _note = note;
        _repository = repository;
        _logger = logger;
        _onDeleted = onDeleted;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void CommitPositionAsync()
    {
        lock (_positionLock)
        {
            _positionDebounceCts?.Cancel();
            _positionDebounceCts = new CancellationTokenSource();
            var token = _positionDebounceCts.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(300, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;

                    _note.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    await _repository.UpdateAsync(_note, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // ignore
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "노트 위치 저장 중 오류 발생");
                }
            }, token);
        }
    }

    public void CommitTextAsync()
    {
        lock (_textLock)
        {
            _textDebounceCts?.Cancel();
            _textDebounceCts = new CancellationTokenSource();
            var token = _textDebounceCts.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(400, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;

                    _note.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    await _repository.UpdateAsync(_note, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // ignore
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "노트 내용 저장 중 오류 발생");
                }
            }, token);
        }
    }

    public async Task DeleteAsync()
    {
        try
        {
            await _repository.DeleteAsync(Id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "노트 삭제 중 오류 발생");
            return;
        }

        _onDeleted(this);
    }
}

