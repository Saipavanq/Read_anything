using System.Windows;
using System.Windows.Input;
using ReadAloud.Services;
using ReadAloud.ViewModels;

namespace ReadAloud.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private string? _recordingAction;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void RecordRead_Click(object sender, RoutedEventArgs e) => StartRecording("read");

    private void RecordStop_Click(object sender, RoutedEventArgs e) => StartRecording("stop");

    private void CancelRecording_Click(object sender, RoutedEventArgs e)
    {
        _recordingAction = null;
        _viewModel.CancelRecording();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Save();
        DialogResult = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void SettingsWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_recordingAction is null)
        {
            return;
        }

        e.Handled = true;

        if (e.Key == Key.Escape)
        {
            _recordingAction = null;
            _viewModel.CancelRecording();
            return;
        }

        var pressed = Keyboard.Modifiers;
        if (!HotkeyService.TryNormalizeKeyEvent(e.Key, e.SystemKey, pressed, out var modifiers, out var key)
            || HotkeyService.IsModifierKey(key))
        {
            return;
        }

        _viewModel.FinishRecording(_recordingAction, modifiers, key);
        _recordingAction = null;
    }

    private void StartRecording(string action)
    {
        _recordingAction = action;
        _viewModel.BeginRecording(action);
    }
}
