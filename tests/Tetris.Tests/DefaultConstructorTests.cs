using Tetris.Services;
using Tetris.ViewModels;

namespace Tetris.Tests;

/// <summary>
/// 各 Service / ViewModel の既定コンストラクタ（%LOCALAPPDATA% 等の実際の保存先を使う版）が
/// 例外なく生成できることを確認するテスト。
/// 生成時点ではいずれも Load（読み取り）のみを行い、ファイルへの書き込みは発生しないため、
/// 実環境の保存先ディレクトリを使っても副作用が無い。
/// </summary>
public class DefaultConstructorTests
{
	/// <summary>パス条件: 既定コンストラクタが例外を投げずインスタンスを返す。</summary>
	[Fact]
	public void HighScoreServiceDefaultConstructorDoesNotThrow()
	{
		var service = new HighScoreService();

		Assert.NotNull(service);
	}

	/// <summary>パス条件: 既定コンストラクタが例外を投げずインスタンスを返す。</summary>
	[Fact]
	public void KeyBindingServiceDefaultConstructorDoesNotThrow()
	{
		var service = new KeyBindingService();

		Assert.NotNull(service);
	}

	/// <summary>パス条件: 既定コンストラクタが例外を投げずインスタンスを返す。</summary>
	[Fact]
	public void SoundSettingsServiceDefaultConstructorDoesNotThrow()
	{
		var service = new SoundSettingsService();

		Assert.NotNull(service);
	}

	/// <summary>パス条件: 既定コンストラクタが例外を投げずインスタンスを返す。</summary>
	[Fact]
	public void AutoRepeatSettingsServiceDefaultConstructorDoesNotThrow()
	{
		var service = new AutoRepeatSettingsService();

		Assert.NotNull(service);
	}

	/// <summary>パス条件: 既定コンストラクタが例外を投げず、実行ファイル基準の Sounds フォルダを指すインスタンスを返す。</summary>
	[Fact]
	public void SoundEffectServiceDefaultConstructorDoesNotThrow()
	{
		var service = new SoundEffectService();

		Assert.NotNull(service);
	}

	/// <summary>
	/// パス条件: 既定コンストラクタ（各 Service の既定コンストラクタを内部で呼ぶ）が例外を投げずインスタンスを返す。
	/// 生成のみで Start() は呼ばないため、ファイルへの書き込みは発生しない。
	/// </summary>
	[Fact]
	public void GameViewModelDefaultConstructorDoesNotThrow()
	{
		var vm = new GameViewModel();

		Assert.NotNull(vm);
	}
}
