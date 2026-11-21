using System.Windows;
using System.Windows.Controls;
using ReadAloud.ViewModels;

namespace ReadAloud.Views;

public partial class VoiceLibraryView : System.Windows.Controls.UserControl
{
    public VoiceLibraryView()
    {
        InitializeComponent();
    }

    private void ClearFilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is VoiceLibraryViewModel viewModel)
        {
            viewModel.LanguageFilter = null;
        }
    }
}
