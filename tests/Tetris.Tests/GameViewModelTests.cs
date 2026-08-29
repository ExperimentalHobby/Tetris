using System.IO;
using Tetris.Input;
using Tetris.Services;
using Tetris.ViewModels;

namespace Tetris.Tests;

/// <summary>
/// <see cref="GameViewModel"/> の状態遷移・コマンド活性制御・イベント発火・DAS/ARR結線を検証するテスト。
/// 副作用（実ファイル書き込み・実音声再生）を避けるため、各Serviceを一時ディレクトリに向けて生成する。
/// </summary>
public class GameViewModelTests : IDisposable
{
	private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

	private GameViewModel CreateViewModel() => new(
		new HighScoreService(_tempDir),
		new AutoRepeatSettingsService(_tempDir),
		new SoundEffectService(_tempDir));

	public void Dispose()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// 開始前は MoveLeftCommand が非活性であることを確認する。
	/// パス条件: StartCommand を実行する前は CanExecute が false。
	/// </summary>
	[Fact]
	public void MoveLeftCommandBeforeStartCannotExecute()
	{
		var vm = CreateViewModel();

		Assert.False(vm.MoveLeftCommand.CanExecute(null));
	}

	/// <summary>
	/// StartCommand 実行後は各操作コマンドが活性化することを確認する。
	/// パス条件: StartCommand 実行後、MoveLeftCommand/RotateCommand/HardDropCommand の CanExecute が true。
	/// </summary>
	[Fact]
	public void MoveLeftCommandAfterStartCanExecute()
	{
		var vm = CreateViewModel();

		vm.StartCommand.Execute(null);

		Assert.True(vm.MoveLeftCommand.CanExecute(null));
		Assert.True(vm.RotateCommand.CanExecute(null));
		Assert.True(vm.HardDropCommand.CanExecute(null));
	}

	/// <summary>
	/// ポーズ中は操作コマンドが再び非活性になることを確認する。
	/// パス条件: 開始後に PauseCommand を実行すると、IsPaused が true になり MoveLeftCommand が非活性になる。
	/// </summary>
	[Fact]
	public void PauseCommandWhilePlayingDisablesPlayCommands()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);

		vm.PauseCommand.Execute(null);

		Assert.True(vm.IsPaused);
		Assert.False(vm.MoveLeftCommand.CanExecute(null));
	}

	/// <summary>
	/// 開始前に PauseCommand を実行しても何も起きないことを確認する。
	/// パス条件: StartCommand を呼ぶ前に PauseCommand.Execute しても IsPaused は false のまま。
	/// </summary>
	[Fact]
	public void PauseCommandBeforeStartDoesNothing()
	{
		var vm = CreateViewModel();

		vm.PauseCommand.Execute(null);

		Assert.False(vm.IsPaused);
	}

	/// <summary>
	/// 再度 PauseCommand を実行するとポーズが解除され、操作コマンドが再び活性化することを確認する。
	/// パス条件: 2 回 PauseCommand を実行すると IsPaused が false に戻り、MoveLeftCommand が活性化する。
	/// </summary>
	[Fact]
	public void PauseCommandExecutedTwiceResumesPlay()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);

		vm.PauseCommand.Execute(null);
		vm.PauseCommand.Execute(null);

		Assert.False(vm.IsPaused);
		Assert.True(vm.MoveLeftCommand.CanExecute(null));
	}

	/// <summary>
	/// StartCommand 実行時に GameStarted イベントが発火することを確認する。
	/// パス条件: StartCommand.Execute() 呼び出しで GameStarted が 1 回発火する。
	/// </summary>
	[Fact]
	public void StartRaisesGameStartedEvent()
	{
		var vm = CreateViewModel();
		int raisedCount = 0;
		vm.GameStarted += (_, _) => raisedCount++;

		vm.StartCommand.Execute(null);

		Assert.Equal(1, raisedCount);
	}

	/// <summary>
	/// 移動操作の結果として StateChanged イベントが発火することを確認する（View の再描画トリガー）。
	/// パス条件: MoveRightCommand.Execute() 呼び出しで StateChanged が発火する。
	/// </summary>
	[Fact]
	public void MoveRightCommandRaisesStateChangedEvent()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);
		bool raised = false;
		vm.StateChanged += (_, _) => raised = true;

		vm.MoveRightCommand.Execute(null);

		Assert.True(raised);
	}

	/// <summary>
	/// ライン消去が保留状態になると LinesClearing イベントが発火することを確認する。
	/// パス条件: Engine のテストシームで満杯行を作り固定した後、RefreshForTest() で LinesClearing が発火する。
	/// </summary>
	[Fact]
	public void LineClearPendingRaisesLinesClearingEvent()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);
		for (int x = 0; x < GameEngine.Columns; x++)
		{
			vm.Engine.Grid[GameEngine.Rows - 1, x] = TetrominoType.J;
		}
		vm.Engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 0, Y = 10 });
		vm.Engine.LockCurrentForTest();
		LinesClearingEventArgs? received = null;
		vm.LinesClearing += (_, e) => received = e;

		vm.RefreshForTest();

		Assert.NotNull(received);
	}

	/// <summary>
	/// 次のピースがスポーンできない状態になると GameOver イベントが発火することを確認する。
	/// パス条件: スポーン位置を塞いだ状態でライン消去を伴わない固定をすると、RefreshForTest() で GameOver が発火する。
	/// </summary>
	[Fact]
	public void GameOverWhenNextPieceCannotSpawnRaisesGameOverEvent()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);
		// スポーン位置(X=3付近、行0-2)の列のみを塞ぎ、行全体は埋めない
		// （行全体を埋めるとLockPiece()のライン検出が先に働き、ゲームオーバーではなくライン消去になってしまうため）。
		for (int y = 0; y < 3; y++)
		{
			for (int x = 3; x <= 6; x++)
			{
				vm.Engine.Grid[y, x] = TetrominoType.J;
			}
		}
		// ライン消去を起こさない位置に固定し、SpawnNext()内でゲームオーバーが確定するようにする。
		vm.Engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 0, Y = 10 });
		vm.Engine.LockCurrentForTest();
		bool raised = false;
		vm.GameOver += (_, _) => raised = true;

		vm.RefreshForTest();

		Assert.True(vm.Engine.IsGameOver);
		Assert.True(raised);
	}

	/// <summary>
	/// ゲームオーバー時にスコアが保存済みハイスコアを上回っていると NewRecord イベントが発火することを確認する。
	/// パス条件: 1ライン消去で得点した後にゲームオーバーにすると、初回（ハイスコア0）なので NewRecord が発火する。
	/// </summary>
	[Fact]
	public void GameOverWithNewHighScoreRaisesNewRecordEvent()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);
		Assert.Equal(0, vm.HighScore); // 初回起動なのでハイスコアは0

		// まず1ライン消去して得点する。
		for (int x = 2; x < GameEngine.Columns; x++)
		{
			vm.Engine.Grid[GameEngine.Rows - 1, x] = TetrominoType.I;
		}
		vm.Engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 0, Y = GameEngine.Rows - 2 });
		vm.Engine.LockCurrentForTest();
		vm.Engine.CommitClear();
		Assert.True(vm.Engine.Score > 0);

		// スポーン位置の列を塞いでゲームオーバーにする。
		for (int y = 0; y < 3; y++)
		{
			for (int x = 3; x <= 6; x++)
			{
				vm.Engine.Grid[y, x] = TetrominoType.J;
			}
		}
		vm.Engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 0, Y = 10 });
		vm.Engine.LockCurrentForTest();
		bool raised = false;
		vm.NewRecord += (_, _) => raised = true;

		vm.RefreshForTest();

		Assert.True(raised);
		Assert.Equal(vm.Engine.Score, vm.HighScore);
	}

	/// <summary>
	/// 消去保留が無い状態で CompleteLineClear() を呼んでも何も起きないことを確認する。
	/// パス条件: 開始直後（保留なし）に CompleteLineClear() を呼んでも Score は 0 のまま。
	/// </summary>
	[Fact]
	public void CompleteLineClearWithoutPendingClearDoesNothing()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);

		vm.CompleteLineClear();

		Assert.Equal(0, vm.Score);
	}

	/// <summary>
	/// 消去保留がある状態で CompleteLineClear() を呼ぶと、消去が確定してスコアに反映されることを確認する。
	/// パス条件: 満杯行を作って固定した後 CompleteLineClear() を呼ぶと IsClearing が false になり Score が加算される。
	/// </summary>
	[Fact]
	public void CompleteLineClearWithPendingClearCommitsAndUpdatesScore()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);
		for (int x = 0; x < GameEngine.Columns; x++)
		{
			vm.Engine.Grid[GameEngine.Rows - 1, x] = TetrominoType.J;
		}
		vm.Engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 0, Y = 10 });
		vm.Engine.LockCurrentForTest();
		Assert.True(vm.Engine.IsClearing);

		vm.CompleteLineClear();

		Assert.False(vm.Engine.IsClearing);
		Assert.True(vm.Score > 0);
	}

	/// <summary>
	/// ApplyAutoRepeatSettings で設定した値が AutoRepeatSettings プロパティに反映されることを確認する。
	/// パス条件: Das=200ms/Arr=30ms を適用すると AutoRepeatSettings がその値になる。
	/// </summary>
	[Fact]
	public void ApplyAutoRepeatSettingsUpdatesAutoRepeatSettingsProperty()
	{
		var vm = CreateViewModel();
		AutoRepeatSettings.TryCreate(TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(30), out var settings, out _);

		vm.ApplyAutoRepeatSettings(settings!);

		Assert.Equal(TimeSpan.FromMilliseconds(200), vm.AutoRepeatSettings.Das);
		Assert.Equal(TimeSpan.FromMilliseconds(30), vm.AutoRepeatSettings.Arr);
	}

	/// <summary>
	/// ApplyAutoRepeatSettings で保存した設定が、同じディレクトリを見る新しい GameViewModel の
	/// コンストラクタで読み込まれることを確認する（永続化の結線）。
	/// パス条件: 保存後に新規生成した GameViewModel の AutoRepeatSettings が保存値と一致する。
	/// </summary>
	[Fact]
	public void ApplyAutoRepeatSettingsPersistsAcrossNewViewModelInstances()
	{
		var vm = CreateViewModel();
		AutoRepeatSettings.TryCreate(TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(30), out var settings, out _);
		vm.ApplyAutoRepeatSettings(settings!);

		var reloaded = CreateViewModel();

		Assert.Equal(TimeSpan.FromMilliseconds(200), reloaded.AutoRepeatSettings.Das);
		Assert.Equal(TimeSpan.FromMilliseconds(30), reloaded.AutoRepeatSettings.Arr);
	}

	/// <summary>
	/// ウィンドウのフォーカス喪失などで KeyUp が届かなかった場合に備え、ReleaseDirectionKeys() で
	/// 左右移動の押下状態を解除できることを確認する。
	/// パス条件: MoveLeftKeyDown 後に ReleaseDirectionKeys() を呼ぶと、DAS を大きく超える
	/// 入力 Tick を進めてもピースが移動しない。
	/// </summary>
	[Fact]
	public void ReleaseDirectionKeysAfterKeyDownStopsAutoRepeat()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);
		vm.MoveLeftKeyDown(); // 押下時の 1 回分はここで移動する
		int xAfterKeyDown = vm.Engine.Current!.X;

		vm.ReleaseDirectionKeys();
		for (int i = 0; i < 60; i++) // 16ms * 60 = 960ms（既定 DAS 170ms を大きく超える）
		{
			vm.AdvanceInputForTest(TimeSpan.FromMilliseconds(16));
		}

		Assert.Equal(xAfterKeyDown, vm.Engine.Current!.X);
	}

	/// <summary>
	/// 対照テスト: ReleaseDirectionKeys() を呼ばなければ、DAS 経過後の入力 Tick で
	/// オートリピートによる移動が発生することを確認する。
	/// （ReleaseDirectionKeysAfterKeyDownStopsAutoRepeat が、そもそもリピートの起きない
	/// 条件で通っているのではないことを保証するためのテスト。）
	/// パス条件: MoveLeftKeyDown 後に解除せず入力 Tick を進めると、押下時の位置より更に左へ動く。
	/// </summary>
	[Fact]
	public void AutoRepeatWithoutReleaseContinuesMovingAfterDas()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);
		vm.MoveLeftKeyDown(); // 押下時の 1 回分はここで移動する
		int xAfterKeyDown = vm.Engine.Current!.X;

		for (int i = 0; i < 60; i++) // 16ms * 60 = 960ms（既定 DAS 170ms を大きく超える）
		{
			vm.AdvanceInputForTest(TimeSpan.FromMilliseconds(16));
		}

		Assert.True(vm.Engine.Current!.X < xAfterKeyDown);
	}
}
