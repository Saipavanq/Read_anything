using System.Windows;
using System.Windows.Input;
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

        if (IsModifierKey(e.Key))
        {
            return;
        }

        var modifiers = ModifierKeys.None;
        var pressed = Keyboard.Modifiers;
        if (pressed.HasFlag(ModifierKeys.Control))
        {
            modifiers |= ModifierKeys.Control;
        }

        if (pressed.HasFlag(ModifierKeys.Shift))
        {
            modifiers |= ModifierKeys.Shift;
        }

        if (pressed.HasFlag(ModifierKeys.Alt))
        {
            modifiers |= ModifierKeys.Alt;
        }

        if (pressed.HasFlag(ModifierKeys.Windows))
        {
            modifiers |= ModifierKeys.Windows;
        }

        _viewModel.FinishRecording(_recordingAction, modifiers, e.Key);
        _recordingAction = null;
    }

    private void StartRecording(string action)
    {
        _recordingAction = action;
        _viewModel.BeginRecording(action);
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftShift or Key.RightShift or
        Key.LeftAlt or Key.RightAlt or
        Key.LWin or Key.RWin;
}
