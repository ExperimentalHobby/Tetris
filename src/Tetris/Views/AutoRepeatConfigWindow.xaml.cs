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
		if (!TryParseSettings(DasTextBox.Text, ArrTextBox.Text, out var settings, out var error))
		{
			ShowError(error!);
			return;
		}

		Result = settings;
		DialogResult = true;
		Close();
	}

	/// <summary>
	/// 入力文字列を検証して DAS/ARR 設定を生成する。WPF に依存しない純粋なロジックとして切り出しており、
	/// 単体テストから直接呼び出せる。
	/// </summary>
	/// <param name="dasText">DAS の入力文字列（ミリ秒）。</param>
	/// <param name="arrText">ARR の入力文字列（ミリ秒）。</param>
	/// <param name="settings">生成できた場合の設定値。失敗時は null。</param>
	/// <param name="error">失敗した場合のエラーメッセージ。成功時は null。</param>
	/// <returns>設定を生成できた場合 true。</returns>
	internal static bool TryParseSettings(string dasText, string arrText, out AutoRepeatSettings? settings, out string? error)
	{
		settings = null;

		if (!double.TryParse(dasText, NumberStyles.Number, CultureInfo.InvariantCulture, out var dasMs)
			|| !double.TryParse(arrText, NumberStyles.Number, CultureInfo.InvariantCulture, out var arrMs))
		{
			error = "DAS/ARRは数値で入力してください。";
			return false;
		}

		TimeSpan das;
		TimeSpan arr;
		try
		{
			das = TimeSpan.FromMilliseconds(dasMs);
			arr = TimeSpan.FromMilliseconds(arrMs);
		}
		// TimeSpan.FromMilliseconds は範囲外で OverflowException、NaN で ArgumentException を投げる。
		// ArgumentOutOfRangeException だけを捕捉していると範囲外入力で未処理例外になるため両方を受け止める。
		catch (Exception ex) when (ex is OverflowException or ArgumentException)
		{
			error = "DAS/ARR の値が大きすぎます。";
			return false;
		}

		return AutoRepeatSettings.TryCreate(das, arr, out settings, out error);
	}

	private void ShowError(string message)
	{
		ErrorText.Text = message;
		ErrorText.Visibility = Visibility.Visible;
	}
}
