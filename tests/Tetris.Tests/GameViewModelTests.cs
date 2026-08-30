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

	/// <summary>
	/// 音量の変更が永続化され、同じディレクトリを見る新しいインスタンスに引き継がれることを確認する。
	/// パス条件: Volume を 0.25 にした後、新規生成した GameViewModel の Volume が 0.25。
	/// </summary>
	[Fact]
	public void VolumeChangePersistsAcrossNewViewModelInstances()
	{
		var vm = CreateViewModel();
		vm.Volume = 0.25;

		var reloaded = CreateViewModel();

		Assert.Equal(0.25, reloaded.Volume);
	}

	/// <summary>
	/// ミュートの変更が永続化され、同じディレクトリを見る新しいインスタンスに引き継がれることを確認する。
	/// パス条件: ToggleMuteCommand 実行後、新規生成した GameViewModel の IsMuted が true。
	/// </summary>
	[Fact]
	public void MuteChangePersistsAcrossNewViewModelInstances()
	{
		var vm = CreateViewModel();
		Assert.False(vm.IsMuted);

		vm.ToggleMuteCommand.Execute(null);

		var reloaded = CreateViewModel();
		Assert.True(reloaded.IsMuted);
	}

	/// <summary>
	/// 重力タイマーの Tick でピースが 1 段落下し、ソフトドロップと違って加点されないことを確認する。
	/// パス条件: AdvanceGravityForTest() 呼び出しで Y が 1 増え、Score は 0 のまま。
	/// </summary>
	[Fact]
	public void GravityTickMovesPieceDownWithoutScoring()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);
		int beforeY = vm.Engine.Current!.Y;

		vm.AdvanceGravityForTest();

		Assert.Equal(beforeY + 1, vm.Engine.Current!.Y);
		Assert.Equal(0, vm.Score);
	}

	/// <summary>
	/// ポーズ中は重力 Tick でピースが落下しないことを確認する。
	/// パス条件: ポーズ後に AdvanceGravityForTest() を呼んでも Y が変わらない。
	/// </summary>
	[Fact]
	public void GravityTickWhilePausedDoesNothing()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);
		vm.PauseCommand.Execute(null);
		int beforeY = vm.Engine.Current!.Y;

		vm.AdvanceGravityForTest();

		Assert.Equal(beforeY, vm.Engine.Current!.Y);
	}

	/// <summary>
	/// MoveLeftCommand / MoveRightCommand がエンジンのピースを実際に動かすことを確認する。
	/// パス条件: 左に 1 マス動いた後、右に動かすと元の位置に戻る。
	/// </summary>
	[Fact]
	public void MoveCommandsMovePieceHorizontally()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);
		int startX = vm.Engine.Current!.X;

		vm.MoveLeftCommand.Execute(null);
		Assert.Equal(startX - 1, vm.Engine.Current!.X);

		vm.MoveRightCommand.Execute(null);
		Assert.Equal(startX, vm.Engine.Current!.X);
	}

	/// <summary>
	/// RotateCommand / RotateCcwCommand が回転姿勢を進める・戻すことを確認する。
	/// パス条件: 時計回りで RotationState が +1、反時計回りで元に戻る。
	/// </summary>
	[Fact]
	public void RotateCommandsChangeRotationState()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);
		// O ピースは回転しても形が変わらないが RotationState は進むため、判定に使える。
		vm.Engine.SetCurrentForTest(new Tetromino(TetrominoType.T) { X = 4, Y = 5 });
		int before = vm.Engine.Current!.RotationState;

		vm.RotateCommand.Execute(null);
		Assert.Equal((before + 1) % 4, vm.Engine.Current!.RotationState);

		vm.RotateCcwCommand.Execute(null);
		Assert.Equal(before, vm.Engine.Current!.RotationState);
	}

	/// <summary>
	/// SoftDropCommand で 1 段落下し 1 点加算されることを確認する。
	/// パス条件: 実行後に Y が 1 増え、Score が 1 になる。
	/// </summary>
	[Fact]
	public void SoftDropCommandMovesDownAndScores()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);
		int beforeY = vm.Engine.Current!.Y;

		vm.SoftDropCommand.Execute(null);

		Assert.Equal(beforeY + 1, vm.Engine.Current!.Y);
		Assert.Equal(1, vm.Score);
	}

	/// <summary>
	/// HardDropCommand でピースが着地・固定され、落下距離 × 2 点が加算されることを確認する。
	/// パス条件: 空の盤面で最上段から落とすと Score が (落下距離 × 2) になり、盤面にブロックが固定される。
	/// </summary>
	[Fact]
	public void HardDropCommandLocksPieceAndScores()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);
		vm.Engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 4, Y = 0 });
		int ghostY = vm.Engine.GhostY()!.Value;
		int distance = ghostY - vm.Engine.Current!.Y;

		vm.HardDropCommand.Execute(null);

		Assert.Equal(distance * 2, vm.Score);
		Assert.Equal(TetrominoType.O, vm.Engine.Grid[GameEngine.Rows - 1, 4]);
	}

	/// <summary>
	/// HoldCommand で現在のピースがホールドされ、1 ピースにつき 1 回だけ有効なことを確認する。
	/// パス条件: 1 回目でホールドされ、2 回目は保管内容が変わらない。
	/// </summary>
	[Fact]
	public void HoldCommandStoresPieceOncePerPiece()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);
		var firstType = vm.Engine.Current!.Type;
		Assert.Null(vm.Engine.HeldType);

		vm.HoldCommand.Execute(null);
		Assert.Equal(firstType, vm.Engine.HeldType);

		vm.HoldCommand.Execute(null); // 設置するまで 2 回目は無効
		Assert.Equal(firstType, vm.Engine.HeldType);
	}

	/// <summary>
	/// 右移動キーの押下でも 1 マス移動し、押しっぱなしのリピートが働くことを確認する。
	/// パス条件: MoveRightKeyDown で 1 マス、その後 DAS を超えて入力 Tick を進めると更に右へ動く。
	/// </summary>
	[Fact]
	public void MoveRightKeyDownThenAutoRepeatMovesFurtherRight()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);
		int startX = vm.Engine.Current!.X;

		vm.MoveRightKeyDown();
		Assert.Equal(startX + 1, vm.Engine.Current!.X);

		for (int i = 0; i < 20; i++) // 16ms * 20 = 320ms（既定 DAS 170ms 超）
		{
			vm.AdvanceInputForTest(TimeSpan.FromMilliseconds(16));
		}

		Assert.True(vm.Engine.Current!.X > startX + 1);

		vm.MoveRightKeyUp();
		int afterRelease = vm.Engine.Current!.X;
		for (int i = 0; i < 20; i++)
		{
			vm.AdvanceInputForTest(TimeSpan.FromMilliseconds(16));
		}
		Assert.Equal(afterRelease, vm.Engine.Current!.X);
	}

	/// <summary>
	/// ポーズ中は入力 Tick でオートリピートが進まないことを確認する。
	/// パス条件: キー押下後にポーズすると、入力 Tick を進めても位置が変わらない。
	/// </summary>
	[Fact]
	public void AdvanceInputWhilePausedDoesNothing()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);
		vm.MoveLeftKeyDown();
		vm.PauseCommand.Execute(null);
		int beforeX = vm.Engine.Current!.X;

		for (int i = 0; i < 20; i++)
		{
			vm.AdvanceInputForTest(TimeSpan.FromMilliseconds(16));
		}

		Assert.Equal(beforeX, vm.Engine.Current!.X);
	}

	/// <summary>
	/// 4 ライン同時消しでも LinesClearing イベントが発火し、消去対象 4 行が通知されることを確認する
	/// （テトリス専用の効果音・演出分岐を通す）。
	/// パス条件: 下 4 行を完成させて固定すると、LinesClearing の Rows が 4 行になる。
	/// </summary>
	[Fact]
	public void TetrisClearRaisesLinesClearingWithFourRows()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);
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

		LinesClearingEventArgs? received = null;
		vm.LinesClearing += (_, e) => received = e;
		vm.RefreshForTest();

		Assert.NotNull(received);
		Assert.Equal(4, received!.Rows.Count);
	}

	/// <summary>
	/// 左移動キーを離すとオートリピートが止まることを確認する。
	/// パス条件: MoveLeftKeyUp 後は入力 Tick を進めてもピースが動かない。
	/// </summary>
	[Fact]
	public void MoveLeftKeyUpStopsAutoRepeat()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);
		vm.MoveLeftKeyDown();

		vm.MoveLeftKeyUp();
		int afterRelease = vm.Engine.Current!.X;
		for (int i = 0; i < 30; i++)
		{
			vm.AdvanceInputForTest(TimeSpan.FromMilliseconds(16));
		}

		Assert.Equal(afterRelease, vm.Engine.Current!.X);
	}

	/// <summary>
	/// 開始前は左右移動キーの押下を受け付けないことを確認する（CanPlay ガード）。
	/// パス条件: StartCommand を呼ぶ前に KeyDown してもピースが存在せず、例外にもならない。
	/// </summary>
	[Fact]
	public void MoveKeyDownBeforeStartDoesNothing()
	{
		var vm = CreateViewModel();

		vm.MoveLeftKeyDown();
		vm.MoveRightKeyDown();

		Assert.Null(vm.Engine.Current);
	}

	/// <summary>最下行を 1 ライン消去して確定させるヘルパー（コンボ検証用）。</summary>
	private static void ClearOneLine(GameViewModel vm)
	{
		int row = GameEngine.Rows - 1;
		for (int x = 2; x < GameEngine.Columns; x++)
		{
			vm.Engine.Grid[row, x] = TetrominoType.I;
		}
		vm.Engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 0, Y = row - 1 });
		vm.Engine.LockCurrentForTest();
		vm.CompleteLineClear();
	}

	/// <summary>
	/// 1 回目の消去ではコンボバッジを出さないことを確認する（ボーナスが付かないため）。
	/// パス条件: 1 ライン消去後、Combo は 1 で IsComboActive は false。
	/// </summary>
	[Fact]
	public void SingleClearDoesNotActivateComboBadge()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);

		ClearOneLine(vm);

		Assert.Equal(1, vm.Combo);
		Assert.False(vm.IsComboActive);
	}

	/// <summary>
	/// 消去が連続すると Combo が増え、2 回目からバッジが表示されることを確認する。
	/// パス条件: 2 連続消去で Combo=2・IsComboActive=true・ComboText が "COMBO x2"。
	/// </summary>
	[Fact]
	public void ConsecutiveClearsActivateComboBadge()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);

		ClearOneLine(vm);
		ClearOneLine(vm);

		Assert.Equal(2, vm.Combo);
		Assert.True(vm.IsComboActive);
		Assert.Equal("COMBO x2", vm.ComboText);
	}

	/// <summary>
	/// ライン消去を伴わない固定でコンボが途切れ、バッジが消えることを確認する。
	/// パス条件: コンボ成立後に消去なしで固定すると Combo=0・IsComboActive=false。
	/// </summary>
	[Fact]
	public void LockWithoutClearResetsComboBadge()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);
		ClearOneLine(vm);
		ClearOneLine(vm);
		Assert.True(vm.IsComboActive);

		// 消去にならない位置で固定する。
		vm.Engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 4, Y = 5 });
		vm.Engine.LockCurrentForTest();
		vm.RefreshForTest();

		Assert.Equal(0, vm.Combo);
		Assert.False(vm.IsComboActive);
	}

	/// <summary>
	/// テトリスを 2 回連続で決めると Back-to-Back バッジが表示されることを確認する。
	/// パス条件: 1 回目は IsBackToBack が false、2 回目で true になる。
	/// </summary>
	[Fact]
	public void ConsecutiveTetrisActivatesBackToBackBadge()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);

		ClearTetris(vm);
		Assert.False(vm.IsBackToBack); // 連続していないため 1 回目は対象外

		ClearTetris(vm);
		Assert.True(vm.IsBackToBack);
	}

	/// <summary>最下 4 行をテトリスで消去して確定させるヘルパー（B2B 検証用）。</summary>
	private static void ClearTetris(GameViewModel vm)
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
		vm.CompleteLineClear();
	}
}
