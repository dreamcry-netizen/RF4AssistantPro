using RF4AssistantPro.Statistics;
using RF4AssistantPro.Cafe;
using RF4AssistantPro.ViewModels;
using RF4AssistantPro.WaterBodies;

namespace RF4AssistantPro.Views;

public partial class WaterBodyGalleryWindow : System.Windows.Window
{
    public WaterBodyGalleryWindow(
        WaterBodyCatalogItem waterBody,
        IEnumerable<CafeSnapshot> cafeSnapshots)
    {
        ArgumentNullException.ThrowIfNull(waterBody);
        ArgumentNullException.ThrowIfNull(cafeSnapshots);
        InitializeComponent();
        DataContext = new WaterBodyDetailViewModel(
            waterBody,
            cafeSnapshots);
    }

    private void TitleBarMouseLeftButtonDown(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximizeWindow();
            return;
        }

        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeWindowClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        WindowState = System.Windows.WindowState.Minimized;
    }

    private void ToggleMaximizeWindowClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        ToggleMaximizeWindow();
    }

    private void CloseWindowClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximizeWindow()
    {
        WindowState = WindowState == System.Windows.WindowState.Maximized
            ? System.Windows.WindowState.Normal
            : System.Windows.WindowState.Maximized;
    }
}
