using Tetris;
using Tetris.Input;

namespace Tetris.Tests;

/// <summary>
/// <see cref="AutoRepeatConfigWindow.TryParseSettings"/> の入力バリデーションを検証するテスト。
/// WPF の Window を生成せず、切り出した純粋ロジックのみを対象にする。
/// </summary>
public class AutoRepeatConfigWindowTests
{
	/// <summary>
	/// 正常な数値入力から設定が生成されることを確認する。
	/// パス条件: "170"/"50" で true を返し、Das/Arr がその値になる。
	/// </summary>
	[Fact]
	public void TryParseSettingsWithValidInputReturnsSettings()
	{
		bool ok = AutoRepeatConfigWindow.TryParseSettings("170", "50", out var settings, out var error);

		Assert.True(ok);
		Assert.Null(error);
		Assert.Equal(TimeSpan.FromMilliseconds(170), settings!.Das);
		Assert.Equal(TimeSpan.FromMilliseconds(50), settings.Arr);
	}

	/// <summary>
	/// DAS は 0 を許容する（押した瞬間からリピート開始）ことを確認する。
	/// パス条件: "0"/"50" で true を返し、Das が 0 になる。
	/// </summary>
	[Fact]
	public void TryParseSettingsAllowsZeroDas()
	{
		bool ok = AutoRepeatConfigWindow.TryParseSettings("0", "50", out var settings, out _);

		Assert.True(ok);
		Assert.Equal(TimeSpan.Zero, settings!.Das);
	}

	/// <summary>
	/// 数値として解釈できない入力を弾くことを確認する。
	/// パス条件: false を返し、数値入力を促すエラーメッセージになる。
	/// </summary>
	[Theory]
	[InlineData("abc", "50")]
	[InlineData("170", "abc")]
	[InlineData("", "50")]
	[InlineData("17 0", "50")]
	public void TryParseSettingsWithNonNumericInputReturnsError(string dasText, string arrText)
	{
		bool ok = AutoRepeatConfigWindow.TryParseSettings(dasText, arrText, out var settings, out var error);

		Assert.False(ok);
		Assert.Null(settings);
		Assert.Equal("DAS/ARRは数値で入力してください。", error);
	}

	/// <summary>
	/// TimeSpan に変換できない巨大な値で例外が外に漏れず、エラーとして扱われることを確認する。
	/// （PR #53 のレビュー指摘で追加された try/catch の回帰を検知するためのテスト。）
	/// パス条件: false を返し、値が大きすぎる旨のエラーメッセージになる。
	/// </summary>
	[Theory]
	[InlineData("100000000000000000000", "50")]
	[InlineData("170", "100000000000000000000")]
	[InlineData("-100000000000000000000", "50")]
	public void TryParseSettingsWithOverflowingValueReturnsError(string dasText, string arrText)
	{
		bool ok = AutoRepeatConfigWindow.TryParseSettings(dasText, arrText, out var settings, out var error);

		Assert.False(ok);
		Assert.Null(settings);
		Assert.Equal("DAS/ARR の値が大きすぎます。", error);
	}

	/// <summary>
	/// AutoRepeatSettings.TryCreate 側の検証エラーがそのまま伝わることを確認する。
	/// パス条件: DAS 負値・ARR 0 以下でそれぞれ対応するエラーメッセージが返る。
	/// </summary>
	[Theory]
	[InlineData("-1", "50", "DASは0ms以上である必要があります。")]
	[InlineData("170", "0", "ARRは0msより大きい値である必要があります。")]
	[InlineData("170", "-1", "ARRは0msより大きい値である必要があります。")]
	public void TryParseSettingsPropagatesRangeValidationError(string dasText, string arrText, string expectedError)
	{
		bool ok = AutoRepeatConfigWindow.TryParseSettings(dasText, arrText, out var settings, out var error);

		Assert.False(ok);
		Assert.Null(settings);
		Assert.Equal(expectedError, error);
	}
}
