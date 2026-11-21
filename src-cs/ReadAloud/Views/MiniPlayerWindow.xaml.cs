using System;
using System.Windows;
using ReadAloud.Models;
using ReadAloud.Services;
using ReadAloud.ViewModels;

namespace ReadAloud.Views;

public partial class MiniPlayerWindow : Window, IMiniPlayerView
{
    public MiniPlayerWindow()
    {
        InitializeComponent();
    }

    public event Action? ExpandRequested;

    public event Action? CloseRequested;

    bool IMiniPlayerView.Topmost
    {
        get => Topmost;
        set => Topmost = value;
    }

    void IMiniPlayerView.Show()
    {
        Position();
        base.Show();
        Activate();
    }

    void IMiniPlayerView.Hide() => base.Hide();

    void IMiniPlayerView.Update(PlaybackState state)
    {
        if (DataContext is MiniPlayerViewModel viewModel)
        {
            viewModel.UpdateState(state);
        }
    }

    private void Position()
    {
        var workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left, workArea.Right - Width - 16);
        Top = Math.Max(workArea.Top, workArea.Bottom - Height - 16);
    }

    private void ExpandButton_Click(object sender, RoutedEventArgs e) => ExpandRequested?.Invoke();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();
}
