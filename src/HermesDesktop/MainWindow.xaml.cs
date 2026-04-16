using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace HermesDesktop;

public partial class MainWindow : Window
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public MainWindow()
    {
        InitializeComponent();
        LoadWindowIcon();
    }

    private void LoadWindowIcon()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (exePath == null) return;

            var hIcon = ExtractIcon(IntPtr.Zero, exePath, 0);
            if (hIcon != IntPtr.Zero && hIcon != (IntPtr)1)
            {
                Icon = Imaging.CreateBitmapSourceFromHIcon(
                    hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                DestroyIcon(hIcon);
            }
        }
        catch
        {
            // Gracefully fall back to default icon
        }
    }
}
