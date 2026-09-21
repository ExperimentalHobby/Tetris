using System.IO;
using Tetris.Input;
using Tetris.Services;
using Tetris.ViewModels;

namespace Tetris.Tests;

/// <summary>
/// <see cref="GameViewModel"/> の音量/ミュート設定の保存デバウンス（Issue #107）を検証するテスト。
/// スライダー操作のたびに毎回ファイル書き込みが発生するのを防ぐため、実際の保存は一定時間の
/// 無操作を待ってから行われる。副作用（実ファイル書き込み）を避けるため、
/// Service は一時ディレクトリに向けて生成する。
/// </summary>
public class GameViewModelSoundSettingsDebounceTests : IDisposable
{
	private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

	private GameViewModel CreateViewModel() => new(
		new HighScoreService(_tempDir),
		new AutoRepeatSettingsService(_tempDir),
		new SoundEffectService(_tempDir),
		new SoundSettingsService(_tempDir));

	private SoundSettingsService CreateSettingsReader() => new(_tempDir);

	public void Dispose()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// パス条件: Volume を変更した直後はまだファイルに反映されない
	/// （即座に保存すると操作のたびに書き込みが発生するため、デバウンスされる）。
	/// </summary>
	[Fact]
	public void ChangingVolumeDoesNotSaveImmediately()
	{
		var vm = CreateViewModel();

		vm.Volume = 0.42;

		var reloaded = CreateSettingsReader().Load();
		Assert.NotEqual(0.42, reloaded.Volume);
	}

	/// <summary>
	/// パス条件: Volume を変更した後、実際のタイマーがデバウンス時間分 Tick すると自動的に保存される。
	/// </summary>
	[Fact]
	public void ChangingVolumeSavesAfterDebounceElapses()
	{
		GameViewModel? vm = null;
		double reloadedVolume = -1;

		StaTestRunner.RunWithMessagePump(
			setup: _ =>
			{
				vm = CreateViewModel();
				vm.Volume = 0.42;
			},
			until: () =>
			{
				var reloaded = CreateSettingsReader().Load();
				if (reloaded.Volume == 0.42)
				{
					reloadedVolume = reloaded.Volume;
					return true;
				}
				return false;
			},
			timeout: TimeSpan.FromSeconds(3));

		Assert.Equal(0.42, reloadedVolume);
	}

	/// <summary>
	/// パス条件: FlushSoundSettings() を呼ぶと、デバウンス時間を待たずに即座に保存される。
	/// </summary>
	[Fact]
	public void FlushSoundSettingsImmediatelyPersistsPendingChange()
	{
		var vm = CreateViewModel();
		vm.Volume = 0.7;

		vm.FlushSoundSettings();

		var reloaded = CreateSettingsReader().Load();
		Assert.Equal(0.7, reloaded.Volume);
	}

	/// <summary>
	/// パス条件: 保留中の変更が無い状態で FlushSoundSettings() を呼んでも例外にならない
	/// （デバウンスタイマーが動いていない場合の早期リターン経路）。
	/// </summary>
	[Fact]
	public void FlushSoundSettingsWithoutPendingChangeDoesNothing()
	{
		var vm = CreateViewModel();

		var ex = Record.Exception(() => vm.FlushSoundSettings());

		Assert.Null(ex);
	}
}
