using Tetris.Input;

namespace Tetris.Tests;

/// <summary>
/// <see cref="AutoRepeatConfigWindow"/> の実際のウィンドウ（コンストラクタ・各ボタンの Click ハンドラ）を
/// 検証するテスト。純粋ロジック（<see cref="AutoRepeatConfigWindow.TryParseSettings"/>）は
/// <see cref="AutoRepeatConfigWindowTests"/> で別途検証済み。
/// </summary>
public class AutoRepeatConfigWindowUiTests
{
	/// <summary>パス条件: コンストラクタ実行後、DAS/ARR の各テキストボックスに渡した設定値が反映される。</summary>
	[Fact]
	public void ConstructorPopulatesFieldsFromCurrentSettings()
	{
		StaTestRunner.Run(() =>
		{
			AutoRepeatSettings.TryCreate(TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(30), out var settings, out _);

			var window = new AutoRepeatConfigWindow(settings!);

			Assert.Equal("200", window.DasTextBox.Text);
			Assert.Equal("30", window.ArrTextBox.Text);
		});
	}

	/// <summary>パス条件: 「既定に戻す」ボタンをクリックすると、フィールドが既定値に戻りエラー表示も消える。</summary>
	[Fact]
	public void ResetButtonClickRestoresDefaultsAndHidesError()
	{
		StaTestRunner.Run(() =>
		{
			AutoRepeatSettings.TryCreate(TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(30), out var settings, out _);
			var window = new AutoRepeatConfigWindow(settings!);
			// 事前にエラー表示状態を作っておき、リセットで消えることを確認できるようにする。
			window.DasTextBox.Text = "abc";
			WpfTestHelpers.Click(WpfTestHelpers.FindButtonByContent(window, "保存"));
			Assert.Equal(System.Windows.Visibility.Visible, window.ErrorText.Visibility);

			WpfTestHelpers.Click(WpfTestHelpers.FindButtonByContent(window, "既定に戻す"));

			Assert.Equal(AutoRepeatController.DefaultDas.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture), window.DasTextBox.Text);
			Assert.Equal(AutoRepeatController.DefaultArr.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture), window.ArrTextBox.Text);
			Assert.Equal(System.Windows.Visibility.Collapsed, window.ErrorText.Visibility);
		});
	}

	/// <summary>パス条件: 不正な入力のまま「保存」をクリックすると、エラーメッセージが表示されダイアログは閉じない。</summary>
	[Fact]
	public void SaveButtonClickWithInvalidInputShowsErrorAndKeepsDialogOpen()
	{
		StaTestRunner.Run(() =>
		{
			var window = new AutoRepeatConfigWindow(AutoRepeatSettings.Default())
			{
				DasTextBox = { Text = "abc" },
			};

			WpfTestHelpers.Click(WpfTestHelpers.FindButtonByContent(window, "保存"));

			Assert.Equal(System.Windows.Visibility.Visible, window.ErrorText.Visibility);
			Assert.Equal("DAS/ARRは数値で入力してください。", window.ErrorText.Text);
			Assert.Null(window.Result);
		});
	}

	/// <summary>
	/// パス条件: 正しい入力で「保存」をクリックすると、ダイアログが閉じ Result に解析結果が入り DialogResult が true になる。
	/// ShowDialog() は自身の Dispatcher で継続してメッセージを処理するため、保存クリックは
	/// ShowDialog() 呼び出し前にキューイングしておく。
	/// </summary>
	[Fact]
	public void SaveButtonClickWithValidInputClosesDialogWithResult()
	{
		StaTestRunner.Run(() =>
		{
			var window = new AutoRepeatConfigWindow(AutoRepeatSettings.Default())
			{
				DasTextBox = { Text = "150" },
				ArrTextBox = { Text = "40" },
			};
			window.Dispatcher.BeginInvoke(new Action(() =>
				WpfTestHelpers.Click(WpfTestHelpers.FindButtonByContent(window, "保存"))));

			bool? dialogResult = window.ShowDialog();

			Assert.True(dialogResult);
			Assert.Equal(TimeSpan.FromMilliseconds(150), window.Result!.Das);
			Assert.Equal(TimeSpan.FromMilliseconds(40), window.Result.Arr);
		});
	}

	/// <summary>
	/// パス条件: 「キャンセル」をクリックすると、ダイアログが閉じ Result は null のまま DialogResult が false になる。
	/// </summary>
	[Fact]
	public void CancelButtonClickClosesDialogWithNullResult()
	{
		StaTestRunner.Run(() =>
		{
			var window = new AutoRepeatConfigWindow(AutoRepeatSettings.Default());
			window.Dispatcher.BeginInvoke(new Action(() =>
				WpfTestHelpers.Click(WpfTestHelpers.FindButtonByContent(window, "キャンセル"))));

			bool? dialogResult = window.ShowDialog();

			Assert.False(dialogResult);
			Assert.Null(window.Result);
		});
	}
}
