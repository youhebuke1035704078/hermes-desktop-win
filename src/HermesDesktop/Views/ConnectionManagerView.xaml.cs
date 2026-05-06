using System.Windows.Controls;
using HermesDesktop.ViewModels;

namespace HermesDesktop.Views;

public partial class ConnectionManagerView : UserControl
{
    public ConnectionManagerView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => KeyPassphraseBox.Clear();
    }

    private void OnKeyPassphraseChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ConnectionManagerViewModel vm && sender is PasswordBox passwordBox)
        {
            vm.EditKeyPassphrase = passwordBox.Password;
        }
    }

    private void OnKeyPassphraseVisibilityChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            passwordBox.Clear();
        }
    }
}
