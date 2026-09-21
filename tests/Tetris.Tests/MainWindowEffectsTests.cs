using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Tetris.Input;
using Tetris.Services;
using Tetris.ViewModels;

namespace Tetris.Tests;

/// <summary>
/// <see cref="MainWindow"/> のライン消去演出・ゲームオーバー演出・NEW RECORD演出・再スタート時のリセットを検証するテスト。
/// GameEngine のテストシーム（SetCurrentForTest/LockCurrentForTest）で決定的な盤面を作って発火させる。
/// </summary>
[Collection("MainWindow")]
public class MainWindowEffectsTests : IDisposable
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

	/// <summary>下 1 行を完成させて固定し、LinesClearing イベントを発火させる。</summary>
	private static void TriggerSingleLineClear(GameViewModel vm)
	{
		int row = GameEngine.Rows - 1;
		for (int x = 2; x < GameEngine.Columns; x++)
		{
			vm.Engine.Grid[row, x] = TetrominoType.I;
		}
		vm.Engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 0, Y = row - 1 });
		vm.Engine.LockCurrentForTest();
		vm.RefreshForTest();
	}

	/// <summary>下 4 行をテトリスで完成させて固定し、LinesClearing イベント（4行）を発火させる。</summary>
	private static void TriggerTetrisClear(GameViewModel vm)
	{
		vm.Engine.Grid[0, 0] = TetrominoType.J; // Perfect Clear にならないようにする
		for (int row = GameEngine.Rows - 4; row < GameEngine.Rows; row++)
		{
			for (int x = 1; x < GameEngine.Columns; x++)
			{
				vm.Engine.Grid[row, x] = TetrominoType.J;
			}
		}
		var verticalI = new Tetromino(TetrominoType.I).Rotated();
		verticalI.X = -verticalI.Blocks().First().X;
		verticalI.Y = (GameEngine.Rows - 4) - verticalI.Blocks().Min(c => c.Y);
		vm.Engine.SetCurrentForTest(verticalI);
		vm.Engine.LockCurrentForTest();
		vm.RefreshForTest();
	}

	/// <summary>スポーン位置の列を塞いでゲームオーバーを発生させる。</summary>
	private static void TriggerGameOver(GameViewModel vm)
	{
		for (int y = 0; y < 3; y++)
		{
			for (int x = 3; x <= 6; x++)
			{
				vm.Engine.Grid[y, x] = TetrominoType.J;
			}
		}
		vm.Engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 0, Y = 10 });
		vm.Engine.LockCurrentForTest();
		vm.RefreshForTest();
	}

	/// <summary>パス条件: ライン消去が発火すると、演出用の要素（閃光・色片）が GameCanvas に追加される。</summary>
	[Fact]
	public void LinesClearingAddsEffectElementsToGameCanvas()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));
			vm.StartCommand.Execute(null);
			int beforeCount = window.GameCanvas.Children.Count;

			TriggerSingleLineClear(vm);

			Assert.True(window.GameCanvas.Children.Count > beforeCount);
		});
	}

	/// <summary>パス条件: テトリス(4ライン)の消去では「TETRIS!」バナーが追加される。</summary>
	[Fact]
	public void TetrisClearShowsTetrisBanner()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));
			vm.StartCommand.Execute(null);

			TriggerTetrisClear(vm);

			Assert.Contains(window.GameCanvas.Children.OfType<TextBlock>(), t => t.Text == "TETRIS!");
		});
	}

	/// <summary>
	/// パス条件: ライン消去演出タイマーが実際に満了すると、演出要素が取り除かれ、
	/// CompleteLineClear() 経由で実際の消去・下詰めが確定する（IsClearing が false になる）。
	/// </summary>
	[Fact]
	public void LineClearTimerElapsingCompletesTheClear()
	{
		GameViewModel? vm = null;
		MainWindow? window = null;
		bool isClearingAfter = true;
		bool hasTetrisBannerAfter = true;

		StaTestRunner.RunWithMessagePump(
			setup: _ =>
			{
				vm = CreateViewModel();
				window = new MainWindow(vm, new KeyBindingService(_tempDir));
				vm.StartCommand.Execute(null);
				TriggerSingleLineClear(vm);
			},
			until: () => vm is not null && !vm.Engine.IsClearing,
			timeout: TimeSpan.FromSeconds(5),
			// window.GameCanvas.Children はスレッドアフィニティを持つため、
			// メッセージポンプを回した同じ STA スレッド上で読み取る。
			afterPump: () =>
			{
				isClearingAfter = vm!.Engine.IsClearing;
				hasTetrisBannerAfter = window!.GameCanvas.Children.OfType<TextBlock>().Any(t => t.Text == "TETRIS!");
			});

		Assert.False(isClearingAfter);
		// 演出要素はクリア済み（プールの盤面セルのみが残る）。
		Assert.False(hasTetrisBannerAfter);
	}

	/// <summary>
	/// パス条件: ゲームオーバーになると、埋没演出（下から灰色ブロックで埋める）が進行し、
	/// 最終的にゲームオーバーオーバーレイが表示される。
	/// </summary>
	[Fact]
	public void GameOverEventuallyShowsGameOverOverlay()
	{
		GameViewModel? vm = null;
		MainWindow? window = null;
		Visibility overlayVisibilityAfter = Visibility.Collapsed;

		StaTestRunner.RunWithMessagePump(
			setup: _ =>
			{
				vm = CreateViewModel();
				window = new MainWindow(vm, new KeyBindingService(_tempDir));
				vm.StartCommand.Execute(null);
				TriggerGameOver(vm);
			},
			until: () => window is not null && window.GameOverOverlay.Visibility == Visibility.Visible,
			timeout: TimeSpan.FromSeconds(5),
			// window.GameOverOverlay はスレッドアフィニティを持つため、
			// メッセージポンプを回した同じ STA スレッド上で読み取る。
			afterPump: () => overlayVisibilityAfter = window!.GameOverOverlay.Visibility);

		Assert.Equal(Visibility.Visible, overlayVisibilityAfter);
	}

	/// <summary>パス条件: スコアがハイスコアを更新するゲームオーバーでは NEW RECORD バナーが追加される。</summary>
	[Fact]
	public void NewHighScoreGameOverShowsNewRecordBanner()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));
			vm.StartCommand.Execute(null);
			Assert.Equal(0, vm.HighScore);

			// まず1ライン消去して得点する。
			for (int x = 2; x < GameEngine.Columns; x++)
			{
				vm.Engine.Grid[GameEngine.Rows - 1, x] = TetrominoType.I;
			}
			vm.Engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 0, Y = GameEngine.Rows - 2 });
			vm.Engine.LockCurrentForTest();
			vm.Engine.CommitClear();
			Assert.True(vm.Engine.Score > 0);

			TriggerGameOver(vm);

			Assert.Contains(window.GameCanvas.Children.OfType<TextBlock>(), t => t.Text == "NEW RECORD!");
		});
	}

	/// <summary>
	/// パス条件: ゲームオーバー演出の途中で再スタートすると、進行中の演出がすべて止まり
	/// オーバーレイが再び非表示に戻る。
	/// </summary>
	[Fact]
	public void RestartingAfterGameOverResetsOverlay()
	{
		GameViewModel? vm = null;
		MainWindow? window = null;
		bool restarted = false;
		Visibility overlayVisibilityAfterRestart = Visibility.Visible;
		double overlayOpacityAfterRestart = 1;

		StaTestRunner.RunWithMessagePump(
			setup: _ =>
			{
				vm = CreateViewModel();
				window = new MainWindow(vm, new KeyBindingService(_tempDir));
				vm.StartCommand.Execute(null);
				TriggerGameOver(vm);
			},
			until: () =>
			{
				if (restarted)
				{
					return true;
				}
				if (window is null || window.GameOverOverlay.Visibility != Visibility.Visible)
				{
					return false;
				}
				// オーバーレイ表示を確認できた時点で、同じ STA スレッド上で再スタートする。
				restarted = true;
				vm!.StartCommand.Execute(null);
				return true;
			},
			timeout: TimeSpan.FromSeconds(5),
			afterPump: () =>
			{
				overlayVisibilityAfterRestart = window!.GameOverOverlay.Visibility;
				overlayOpacityAfterRestart = window.GameOverOverlay.Opacity;
			});

		Assert.True(restarted);
		Assert.Equal(Visibility.Collapsed, overlayVisibilityAfterRestart);
		Assert.Equal(0, overlayOpacityAfterRestart);
	}
}
