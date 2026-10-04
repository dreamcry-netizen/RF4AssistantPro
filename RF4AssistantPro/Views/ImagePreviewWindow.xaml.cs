using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace RF4AssistantPro.Views;

public partial class ImagePreviewWindow : System.Windows.Window
{
    public ImagePreviewWindow(string imagePath)
    {
        InitializeComponent();

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(Path.GetFullPath(imagePath));
        image.EndInit();
        image.Freeze();
        PreviewImage.Source = image;
    }

    private void CloseClick(object sender, System.Windows.RoutedEventArgs e)
    {
        Close();
    }

    private void WindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}