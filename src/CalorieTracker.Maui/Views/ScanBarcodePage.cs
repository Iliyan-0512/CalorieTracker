using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace CalorieTracker.Maui.Views;

/// <summary>Сканира баркод с камерата и връща кода към екрана "Добави храна".</summary>
public class ScanBarcodePage : ContentPage
{
    private readonly CameraBarcodeReaderView _reader;
    private bool _handled;

    public ScanBarcodePage()
    {
        Title = "Сканирай баркод";
        Style = (Style)Application.Current!.Resources["PageBackground"];

        _reader = new CameraBarcodeReaderView
        {
            Options = new BarcodeReaderOptions
            {
                // EAN-13 / EAN-8 / UPC – баркодовете на храните
                Formats = BarcodeFormats.OneDimensional,
                AutoRotate = true,
                Multiple = false
            },
            IsDetecting = false
        };
        _reader.BarcodesDetected += OnBarcodesDetected;

        var torch = new Button { Text = "Фенер", Margin = 16, HorizontalOptions = LayoutOptions.Center };
        torch.Clicked += (_, _) => _reader.IsTorchOn = !_reader.IsTorchOn;

        Content = new Grid
        {
            RowDefinitions = [new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)],
            Children =
            {
                _reader,
                new Label
                {
                    Text = "Насочете камерата към баркода на опаковката",
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(16, 12, 16, 0)
                }.Row(1),
                torch.Row(2)
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (await Permissions.RequestAsync<Permissions.Camera>() != PermissionStatus.Granted)
        {
            await DisplayAlertAsync("Няма достъп", "Разрешете достъп до камерата, за да сканирате баркодове.", "OK");
            await Shell.Current.GoToAsync("..");
            return;
        }

        _handled = false;
        _reader.IsDetecting = true;
    }

    protected override void OnDisappearing()
    {
        _reader.IsDetecting = false;
        _reader.IsTorchOn = false;
        base.OnDisappearing();
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var code = e.Results.FirstOrDefault()?.Value;
        if (_handled || string.IsNullOrWhiteSpace(code)) return;
        _handled = true;

        // Събитието идва от фонова нишка
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            _reader.IsDetecting = false;
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch (FeatureNotSupportedException) { }
            await Shell.Current.GoToAsync("..", new Dictionary<string, object> { ["scannedBarcode"] = code });
        });
    }
}

internal static class GridExtensions
{
    public static T Row<T>(this T view, int row) where T : BindableObject
    {
        Grid.SetRow(view, row);
        return view;
    }
}
