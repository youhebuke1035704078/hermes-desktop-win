using System.Windows.Controls;
using System.Windows.Input;
using HermesDesktop.ViewModels;

namespace HermesDesktop.Views;

public partial class AppUpdaterBadge : UserControl
{
    public AppUpdaterBadge()
    {
        InitializeComponent();
    }

    private void Pill_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AppUpdaterViewModel vm)
        {
            vm.ToggleFlyoutCommand.Execute(null);
        }
    }
}
