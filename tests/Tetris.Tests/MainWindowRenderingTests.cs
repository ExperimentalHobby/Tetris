using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Shapes;
using Tetris.Input;
using Tetris.Services;
using Tetris.ViewModels;
using Path = System.IO.Path;

namespace Tetris.Tests;

/// <summary>
/// <see cref="MainWindow"/> の盤面描画（グリッド線・セルプール・NEXT/HOLDプレビュー）を検証するテスト。
/// 実ファイルへの副作用を避けるため、GameViewModel は一時ディレクトリに向けた Service で生成する。
/// </summary>
[Collection("MainWindow")]
public class MainWindowRenderingTests : IDisposable
{
	private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

	private GameViewModel CreateViewModel() => new(
		new HighScoreService(_tempDir),
		new AutoRepeatSettingsService(_tempDir),
		new SoundEffectService(_tempDir),
		new SoundSettingsService(_tempDir));

	private MainWindow CreateWindow() => new(CreateViewModel(), new KeyBindingService(_tempDir));

	public void Dispose()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
		GC.SuppressFinalize(this);
	}

	/// <summary>パス条件: コンストラクタ実行後、盤面と同じマス数(10×20)のグリッド線が描画される。</summary>
	[Fact]
	public void ConstructorDrawsGridLinesForEveryCell()
	{
		StaTestRunner.Run(() =>
		{
			var window = CreateWindow();

			Assert.Equal(GameEngine.Rows * GameEngine.Columns, window.GridCanvas.Children.Count);
		});
	}

	/// <summary>パス条件: コンストラクタ実行後、盤面セル用プールが GameCanvas に確保される（開始前は全て非表示）。</summary>
	[Fact]
	public void ConstructorInitializesCellPoolAllHiddenBeforeStart()
	{
		StaTestRunner.Run(() =>
		{
			var window = CreateWindow();

			Assert.True(window.GameCanvas.Children.Count >= GameEngine.Rows * GameEngine.Columns);
			Assert.All(window.GameCanvas.Children.OfType<Rectangle>(), r => Assert.Equal(Visibility.Collapsed, r.Visibility));
		});
	}

	/// <summary>パス条件: Start() 後、固定ブロックと落下中ピース・ゴーストの分だけプールセルが可視化される。</summary>
	[Fact]
	public void AfterStartSomeCellPoolRectanglesBecomeVisible()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));
			vm.StartCommand.Execute(null);

			int visibleCount = window.GameCanvas.Children.OfType<Rectangle>().Count(r => r.Visibility == Visibility.Visible);

			// 現在ピース(4マス)+ゴースト(最大4マス)は必ず描画される。
			Assert.True(visibleCount > 0);
		});
	}

	/// <summary>パス条件: NEXTキューの先読み件数分だけ NextCanvas に図形が描画される。</summary>
	[Fact]
	public void AfterStartNextCanvasContainsPreviewShapes()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));
			vm.StartCommand.Execute(null);

			Assert.True(window.NextCanvas.Children.Count > 0);
		});
	}

	/// <summary>パス条件: ホールドが空の間は HoldCanvas に何も描画されない。</summary>
	[Fact]
	public void BeforeHoldingHoldCanvasIsEmpty()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));
			vm.StartCommand.Execute(null);

			Assert.Empty(window.HoldCanvas.Children);
		});
	}

	/// <summary>パス条件: ホールド後は HoldCanvas にプレビュー図形が描画される。</summary>
	[Fact]
	public void AfterHoldingHoldCanvasContainsPreviewShapes()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));
			vm.StartCommand.Execute(null);

			vm.HoldCommand.Execute(null);

			Assert.NotEmpty(window.HoldCanvas.Children);
		});
	}

	/// <summary>パス条件: UpdateControlsHelpText() が現在のキーコンフィグに応じた操作説明文を組み立てる。</summary>
	[Fact]
	public void ConstructorSetsControlsHelpTextFromKeyBindings()
	{
		StaTestRunner.Run(() =>
		{
			var window = new MainWindow(CreateViewModel(), new KeyBindingService(_tempDir));

			Assert.Contains("←", window.ControlsHelpText.Text);
			Assert.Contains("回転", window.ControlsHelpText.Text);
		});
	}

	/// <summary>
	/// パス条件: 開始操作のキーをリマップした状態でウィンドウを生成すると、
	/// GameViewModel.StartKeyLabel がリマップ後のキー表示に一致する（Issue #105）。
	/// </summary>
	[Fact]
	public void ConstructorSetsViewModelStartKeyLabelFromKeyBindings()
	{
		StaTestRunner.Run(() =>
		{
			var keyBindingService = new KeyBindingService(_tempDir);
			var customBindings = KeyBindings.Default();
			customBindings.TrySetKey(GameAction.Start, System.Windows.Input.Key.X);
			keyBindingService.Save(customBindings);
			var vm = CreateViewModel();

			_ = new MainWindow(vm, keyBindingService);

			Assert.Equal("X", vm.StartKeyLabel);
			Assert.Equal("X で開始", vm.Status);
		});
	}
}
