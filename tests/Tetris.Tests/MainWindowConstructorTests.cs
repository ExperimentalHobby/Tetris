using System.IO;
using Tetris.Input;
using Tetris.Services;
using Tetris.ViewModels;

namespace Tetris.Tests;

/// <summary>
/// <see cref="MainWindow"/> のコンストラクタ配線を検証するテスト。
/// 実ファイルへの副作用（%LOCALAPPDATA% への書き込み・読み込み）を避けるため、
/// テスト用の internal コンストラクタで GameViewModel / KeyBindingService を注入する。
/// </summary>
[Collection("MainWindow")]
public class MainWindowConstructorTests : IDisposable
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

	/// <summary>パス条件: 注入した GameViewModel が DataContext に設定される。</summary>
	[Fact]
	public void ConstructorSetsDataContextToInjectedViewModel()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));

			Assert.Same(vm, window.DataContext);
		});
	}

	/// <summary>
	/// パス条件: 既定（パラメータレス）のコンストラクタが例外なくインスタンスを生成できる。
	/// コンストラクタ内では各 Service の Load()（読み取りのみ）しか行わないため、
	/// 実際の %LOCALAPPDATA% を使っても書き込みは発生しない。Start() は呼ばない。
	/// </summary>
	[Fact]
	public void DefaultConstructorDoesNotThrow()
	{
		StaTestRunner.Run(() =>
		{
			var window = new MainWindow();

			Assert.NotNull(window);
		});
	}
}
