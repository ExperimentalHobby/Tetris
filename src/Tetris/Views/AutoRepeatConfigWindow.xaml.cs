using System.Globalization;
using System.Windows;
using Tetris.Input;

namespace Tetris;

/// <summary>
/// DAS/ARR（キー押しっぱなし時の入力リピート）を設定するモーダルダイアログ。
/// 「保存」を押すまでは <see cref="Services.AutoRepeatSettingsService"/> への永続化は行わない。
/// </summary>
public partial class AutoRepeatConfigWindow : Window
{
    /// <summary>「保存」で閉じられた場合に確定した設定値。それ以外（キャンセル等）は null。</summary>
    public AutoRepeatSettings? Result { get; private set; }

    public AutoRepeatConfigWindow(AutoRepeatSettings current)
    {
        InitializeComponent();
        SetFields(current);
    }

    private void SetFields(AutoRepeatSettings settings)
    {
        DasTextBox.Text = settings.Das.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
        ArrTextBox.Text = settings.Arr.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void OnResetClick(object sender, RoutedEventArgs e) => SetFields(AutoRepeatSettings.Default());

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(DasTextBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var dasMs)
            || !double.TryParse(ArrTextBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var arrMs))
        {
            ShowError("DAS/ARRは数値で入力してください。");
            return;
        }

        TimeSpan das;
        TimeSpan arr;
        try
        {
            das = TimeSpan.FromMilliseconds(dasMs);
            arr = TimeSpan.FromMilliseconds(arrMs);
        }
        catch (ArgumentOutOfRangeException)
        {
            ShowError("DAS/ARR の値が大きすぎます。");
            return;
        }

        if (!AutoRepeatSettings.TryCreate(das, arr, out var settings, out var error))
        {
            ShowError(error!);
            return;
        }

        Result = settings;
        DialogResult = true;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
