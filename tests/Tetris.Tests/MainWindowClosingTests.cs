using System.IO;
using Tetris.Input;
using Tetris.Services;
using Tetris.ViewModels;

namespace Tetris.Tests;

/// <summary>
/// <see cref="MainWindow"/> を閉じる際に、デバウンス中の音量/ミュート設定の保存が
/// 失われないことを検証するテスト（Issue #107）。
/// </summary>
[Collection("MainWindow")]
public class MainWindowClosingTests : IDisposable
{
	private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

	private GameViewModel CreateViewModel() => new(
		new HighScoreService(_tempDir),
		new AutoRepeatSettingsService(_tempDir),
		new SoundEffectService(_tempDir),
		new SoundSettingsService(_tempDir));

	public void Dispose()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// パス条件: デバウンス中（未保存）の音量変更があっても、ウィンドウを閉じると即座に保存される。
	/// </summary>
	[Fact]
	public void ClosingWindowFlushesPendingSoundSettings()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));
			vm.Volume = 0.55;

			window.Close();

			var reloaded = new SoundSettingsService(_tempDir).Load();
			Assert.Equal(0.55, reloaded.Volume);
		});
	}
}
