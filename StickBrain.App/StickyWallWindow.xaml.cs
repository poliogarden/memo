using System;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace StickBrain.App;

public partial class StickyWallWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly StickyWallViewModel _vm;

    public StickyWallWindow(StickyWallViewModel vm, IServiceProvider services)
    {
        _services = services;
        _vm = vm;
        InitializeComponent();
        DataContext = vm;
    }

    private void NewNoteButton_OnClick(object sender, RoutedEventArgs e)
    {
        // 상단 버튼을 눌러도 캡처 창을 띄워서 동일한 흐름 사용
        var capture = _services.GetRequiredService<CaptureWindow>();
        capture.Show();
        capture.Activate();
    }

    private void CascadeButton_OnClick(object sender, RoutedEventArgs e)
    {
        // 간단한 캐스케이드: 기준 위치에서 일정 간격으로 대각선 배치
        const double startX = 40;
        const double startY = 40;
        const double offsetX = 28;
        const double offsetY = 24;

        var index = 0;
        foreach (var note in _vm.Notes)
        {
            note.X = startX + offsetX * index;
            note.Y = startY + offsetY * index;
            note.CommitPositionAsync();
            index++;
        }
    }

    private void GridButton_OnClick(object sender, RoutedEventArgs e)
    {
        // 간단한 격자 배치: 고정 크기 셀에 순서대로 정렬
        const double marginLeft = 40;
        const double marginTop = 60;
        const double cellWidth = 260;  // 노트(220) + 여백
        const double cellHeight = 200; // 노트(160) + 여백
        const int columns = 4;

        var notes = _vm.Notes.ToList();
        for (var i = 0; i < notes.Count; i++)
        {
            var col = i % columns;
            var row = i / columns;

            var x = marginLeft + col * cellWidth;
            var y = marginTop + row * cellHeight;

            notes[i].X = x;
            notes[i].Y = y;
            notes[i].CommitPositionAsync();
        }
    }
}

