using System.Windows;

namespace StickBrain.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // 이 윈도우는 사용되지 않지만, WPF 템플릿 구조 유지를 위해 남겨둔다.
        Hide();
    }
}