using System.IO;
using Tetris.Services;
using Tetris.ViewModels;

namespace Tetris.Tests;

/// <summary>
/// <see cref="GameViewModel"/> の重力タイマー / 入力タイマーが、フェイクではなく実際の
/// <see cref="System.Windows.Threading.DispatcherTimer"/> の Tick 経由で正しく配線されていることを検証するテスト。
/// 他の GameViewModel テストは AdvanceGravityForTest / AdvanceInputForTest で内部ロジックのみを直接呼んでいるため、
/// OnTick / OnInputTick 自体（イベントハンドラとしての配線）はここでのみ検証する。
/// 副作用（実ファイル書き込み・実音声再生）を避けるため、各Serviceを一時ディレクトリに向けて生成する。
/// </summary>
public class GameViewModelTimerTests : IDisposable
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
	/// パス条件: Start() 後、実際の重力タイマーが Tick すると OnTick 経由で AdvanceGravity が呼ばれ、
	/// 現在ピースの Y 座標が進む。
	/// </summary>
	[Fact]
	public void GravityTimerRealTickMovesPieceDown()
	{
		GameViewModel? vm = null;
		int initialY = 0;

		StaTestRunner.RunWithMessagePump(
			setup: _ =>
			{
				vm = CreateViewModel();
				vm.StartCommand.Execute(null);
				initialY = vm.Engine.Current!.Y;
			},
			until: () => vm!.Engine.Current is not null && vm.Engine.Current.Y > initialY,
			timeout: TimeSpan.FromSeconds(5));

		Assert.True(vm!.Engine.Current!.Y > initialY);
	}

	/// <summary>
	/// パス条件: MoveLeftKeyDown() 後、実際の入力タイマーが Tick を重ねると OnInputTick 経由で
	/// DAS/ARR のリピート移動が発生し、最初の即時移動よりさらに左へ進む。
	/// </summary>
	[Fact]
	public void InputTimerRealTicksProduceDasArrRepeatMovement()
	{
		GameViewModel? vm = null;
		int afterImmediateMoveX = 0;

		StaTestRunner.RunWithMessagePump(
			setup: _ =>
			{
				vm = CreateViewModel();
				vm.StartCommand.Execute(null);
				vm.MoveLeftKeyDown();
				afterImmediateMoveX = vm.Engine.Current!.X;
			},
			until: () => vm!.Engine.Current is not null && vm.Engine.Current.X < afterImmediateMoveX,
			timeout: TimeSpan.FromSeconds(5));

		Assert.True(vm!.Engine.Current!.X < afterImmediateMoveX);
	}
}
