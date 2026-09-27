using System.Windows.Media.Imaging;

namespace DebugManager.Services;

public static class ImageLoader
{
    public static BitmapImage LoadCharacter(string fileName)
    {
        return Load(
            $"pack://application:,,,/DebugManager;component/Assets/Characters/{fileName}");
    }

    public static BitmapImage LoadVehicle(string fileName)
    {
        return Load(
            $"pack://application:,,,/DebugManager;component/Assets/Vehicles/{fileName}");
    }

    private static BitmapImage Load(string uri)
    {
        return new BitmapImage(
            new Uri(uri, UriKind.Absolute));
    }
}