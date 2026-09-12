using System.Windows.Input;
using Tetris.Input;

namespace Tetris.Tests;

/// <summary>
/// <see cref="KeyDisplay"/> の表示文字列変換を検証するテスト。
/// </summary>
public class KeyDisplayTests
{
	/// <summary>
	/// 矢印キー・Enter・Space は専用の記号・文字列に変換されることを確認する。
	/// パス条件: 各キーに対応する短い表示文字列が返る。
	/// </summary>
	[Theory]
	[InlineData(Key.Left, "←")]
	[InlineData(Key.Right, "→")]
	[InlineData(Key.Up, "↑")]
	[InlineData(Key.Down, "↓")]
	[InlineData(Key.Return, "Enter")]
	[InlineData(Key.Space, "Space")]
	public void ToDisplayStringConvertsSpecialKeysToSymbols(Key key, string expected)
	{
		var result = KeyDisplay.ToDisplayString(key);

		Assert.Equal(expected, result);
	}

	/// <summary>
	/// 記号化対象外のキーは列挙値名がそのまま返る（フォールバック）ことを確認する。
	/// パス条件: Key.Z / Key.C / Key.P / Key.M は key.ToString() と一致する。
	/// </summary>
	[Theory]
	[InlineData(Key.Z)]
	[InlineData(Key.C)]
	[InlineData(Key.P)]
	[InlineData(Key.M)]
	public void ToDisplayStringFallsBackToEnumNameForOtherKeys(Key key)
	{
		var result = KeyDisplay.ToDisplayString(key);

		Assert.Equal(key.ToString(), result);
	}
}
