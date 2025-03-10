using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ROMMend.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void MinimizeWindow(object sender, RoutedEventArgs e)
    {
        if (WindowState != WindowState.Minimized)
        {
            WindowState = WindowState.Minimized;
        }
    }

    private void MaximizeWindow(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseWindow(object sender, RoutedEventArgs e)
    {
        Close();
    }
}