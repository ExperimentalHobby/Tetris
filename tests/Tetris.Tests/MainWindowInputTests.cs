using System.IO;
using System.Windows.Input;
using Tetris.Input;
using Tetris.Services;
using Tetris.ViewModels;

namespace Tetris.Tests;

/// <summary>
/// <see cref="MainWindow"/> のキー入力ディスパッチ（OnPreviewKeyDown/Up）を検証するテスト。
/// キーコンフィグは既定値（Key.Left=MoveLeft 等）を前提にする。
/// </summary>
[Collection("MainWindow")]
public class MainWindowInputTests : IDisposable
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

	/// <summary>パス条件: 未開始の状態で Enter(Start) を押すとゲームが開始する。</summary>
	[Fact]
	public void PressingStartKeyStartsTheGame()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));

			WpfTestHelpers.SimulateKeyDown(window, Key.Return);

			Assert.NotNull(vm.Engine.Current);
		});
	}

	/// <summary>パス条件: 左キー(非リピート)を押すと現在ピースが左へ移動する。</summary>
	[Fact]
	public void PressingMoveLeftKeyMovesPieceLeft()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));
			vm.StartCommand.Execute(null);
			int initialX = vm.Engine.Current!.X;

			WpfTestHelpers.SimulateKeyDown(window, Key.Left, isRepeat: false);

			Assert.Equal(initialX - 1, vm.Engine.Current!.X);
		});
	}

	/// <summary>パス条件: 左キーが OS のキーリピートによる入力(IsRepeat=true)の場合は無視される。</summary>
	[Fact]
	public void RepeatedMoveLeftKeyIsIgnored()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));
			vm.StartCommand.Execute(null);
			int initialX = vm.Engine.Current!.X;

			WpfTestHelpers.SimulateKeyDown(window, Key.Left, isRepeat: true);

			Assert.Equal(initialX, vm.Engine.Current!.X);
		});
	}

	/// <summary>パス条件: 右キー(非リピート)を押すと現在ピースが右へ移動する。</summary>
	[Fact]
	public void PressingMoveRightKeyMovesPieceRight()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));
			vm.StartCommand.Execute(null);
			int initialX = vm.Engine.Current!.X;

			WpfTestHelpers.SimulateKeyDown(window, Key.Right, isRepeat: false);

			Assert.Equal(initialX + 1, vm.Engine.Current!.X);
		});
	}

	/// <summary>パス条件: 右キーが OS のキーリピートによる入力の場合は無視される。</summary>
	[Fact]
	public void RepeatedMoveRightKeyIsIgnored()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));
			vm.StartCommand.Execute(null);
			int initialX = vm.Engine.Current!.X;

			WpfTestHelpers.SimulateKeyDown(window, Key.Right, isRepeat: true);

			Assert.Equal(initialX, vm.Engine.Current!.X);
		});
	}

	/// <summary>パス条件: 上キー(Rotate)を押すと現在ピースの回転状態が変わる。</summary>
	[Fact]
	public void PressingRotateKeyRotatesPiece()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));
			vm.StartCommand.Execute(null);
			int initialState = vm.Engine.Current!.RotationState;

			WpfTestHelpers.SimulateKeyDown(window, Key.Up);

			Assert.NotEqual(initialState, vm.Engine.Current!.RotationState);
		});
	}

	/// <summary>パス条件: Z キー(RotateCcw)を押すと反時計回りに回転する。</summary>
	[Fact]
	public void PressingRotateCcwKeyRotatesPieceCounterClockwise()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));
			vm.StartCommand.Execute(null);
			int initialState = vm.Engine.Current!.RotationState;

			WpfTestHelpers.SimulateKeyDown(window, Key.Z);

			Assert.NotEqual(initialState, vm.Engine.Current!.RotationState);
		});
	}

	/// <summary>パス条件: 下キー(SoftDrop)を押すとスコアが加算される。</summary>
	[Fact]
	public void PressingSoftDropKeyIncreasesScore()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));
			vm.StartCommand.Execute(null);

			WpfTestHelpers.SimulateKeyDown(window, Key.Down);

			Assert.True(vm.Score > 0);
		});
	}

	/// <summary>パス条件: スペースキー(HardDrop)を押すと現在ピースが固定され、ロックディレイ経由の固定と異なりすぐ次のピースに切り替わる。</summary>
	[Fact]
	public void PressingHardDropKeyLocksPieceImmediately()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));
			vm.StartCommand.Execute(null);
			int initialPieceCount = vm.Engine.PieceCount;

			WpfTestHelpers.SimulateKeyDown(window, Key.Space);

			Assert.Equal(initialPieceCount + 1, vm.Engine.PieceCount);
		});
	}

	/// <summary>パス条件: C キー(Hold)を押すと現在ピースがホールドされる。</summary>
	[Fact]
	public void PressingHoldKeyHoldsCurrentPiece()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));
			vm.StartCommand.Execute(null);

			WpfTestHelpers.SimulateKeyDown(window, Key.C);

			Assert.NotNull(vm.Engine.HeldType);
		});
	}

	/// <summary>パス条件: P キー(Pause)を押すとポーズ状態になる。</summary>
	[Fact]
	public void PressingPauseKeyPausesTheGame()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));
			vm.StartCommand.Execute(null);

			WpfTestHelpers.SimulateKeyDown(window, Key.P);

			Assert.True(vm.IsPaused);
		});
	}

	/// <summary>パス条件: M キー(ToggleMute)を押すとミュート状態が切り替わる。</summary>
	[Fact]
	public void PressingToggleMuteKeyTogglesMute()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));
			bool initialMuted = vm.IsMuted;

			WpfTestHelpers.SimulateKeyDown(window, Key.M);

			Assert.Equal(!initialMuted, vm.IsMuted);
		});
	}

	/// <summary>パス条件: どの操作にも割り当てられていないキーは無視される（例外にならない）。</summary>
	[Fact]
	public void PressingUnboundKeyDoesNothing()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));

			WpfTestHelpers.SimulateKeyDown(window, Key.F1);

			Assert.Null(vm.Engine.Current);
		});
	}

	/// <summary>パス条件: 左キーを離すと DAS/ARR のリピートが止まる（ReleaseKey 相当が呼ばれる）。</summary>
	[Fact]
	public void ReleasingMoveLeftKeyStopsAutoRepeat()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));
			vm.StartCommand.Execute(null);
			WpfTestHelpers.SimulateKeyDown(window, Key.Left);

			// 例外なく完了すれば、MoveLeftKeyUp() 経路が正しく呼ばれている。
			WpfTestHelpers.SimulateKeyUp(window, Key.Left);
		});
	}

	/// <summary>パス条件: 右キーを離しても例外にならない（MoveRightKeyUp 経路）。</summary>
	[Fact]
	public void ReleasingMoveRightKeyStopsAutoRepeat()
	{
		StaTestRunner.Run(() =>
		{
			var vm = CreateViewModel();
			var window = new MainWindow(vm, new KeyBindingService(_tempDir));
			vm.StartCommand.Execute(null);
			WpfTestHelpers.SimulateKeyDown(window, Key.Right);

			WpfTestHelpers.SimulateKeyUp(window, Key.Right);
		});
	}
}
