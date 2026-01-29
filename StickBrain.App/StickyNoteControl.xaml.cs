using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StickBrain.App;

public partial class StickyNoteControl : UserControl
{
    private bool _isDragging;
    private Point _dragStart;

    public StickyNoteControl()
    {
        InitializeComponent();
    }

    private StickyNoteViewModel? Vm => DataContext as StickyNoteViewModel;

    private void Border_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm == null) return;

        // 텍스트박스 안을 클릭한 경우에는 드래그 대신 편집을 우선시
        if (e.OriginalSource is System.Windows.Controls.TextBox)
        {
            return;
        }

        _isDragging = true;
        _dragStart = e.GetPosition(null);
        // 드래그는 Border가 계속 마우스 이벤트를 받도록 Border에 캡처
        if (sender is IInputElement element)
        {
            Mouse.Capture(element);
        }
        e.Handled = true;
    }

    private void Border_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || Vm == null) return;

        var current = e.GetPosition(null);
        var dx = current.X - _dragStart.X;
        var dy = current.Y - _dragStart.Y;

        Vm.X += dx;
        Vm.Y += dy;

        _dragStart = current;
    }

    private void Border_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;

        _isDragging = false;
        // 마우스 캡처 해제
        Mouse.Capture(null);
        Vm?.CommitPositionAsync();
    }

    private void TextBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        Vm?.CommitTextAsync();
    }

    private async void DeleteButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;

        try
        {
            await Vm.DeleteAsync();
        }
        catch
        {
            // 예외는 뷰모델 내부에서 로깅 처리
        }
    }
}

