using System.Linq;
using Tetris;

namespace Tetris.Tests;

/// <summary>
/// <see cref="GameEngine"/> の進行・移動・ライン消去ロジックを検証するテスト。
/// </summary>
public class GameEngineTests
{
	/// <summary>開始済みのエンジンを生成するヘルパー。</summary>
	private static GameEngine StartedEngine()
	{
		var engine = new GameEngine();
		engine.Start();
		return engine;
	}

	/// <summary>最下行を 1 ライン分だけ完成させて確定するヘルパー（コンボ・B2B テスト用）。</summary>
	private static void ClearSingleLineAtBottom(GameEngine engine)
	{
		int row = GameEngine.Rows - 1;
		for (int x = 2; x < GameEngine.Columns; x++)
		{
			engine.Grid[row, x] = TetrominoType.I;
		}
		engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 0, Y = row - 1 });
		engine.LockCurrentForTest();
		engine.CommitClear();
	}

	/// <summary>
	/// 最下 4 行をテトリス(4ライン同時消し)で完成させて確定するヘルパー（コンボ・B2B テスト用）。
	/// 最上段(行0)に残存ブロックを置き、Perfect Clear（全消し）にならないようにする
	/// （このヘルパーは「通常のテトリス」を意図したもので、Perfect Clear用の別テストと区別するため）。
	/// </summary>
	private static void ClearTetrisAtBottom(GameEngine engine)
	{
		engine.Grid[0, 0] = TetrominoType.J;

		for (int row = GameEngine.Rows - 4; row < GameEngine.Rows; row++)
		{
			for (int x = 1; x < GameEngine.Columns; x++)
			{
				engine.Grid[row, x] = TetrominoType.J;
			}
		}

		var verticalI = new Tetromino(TetrominoType.I).Rotated();
		var cells = verticalI.Blocks().ToList();
		int localColumn = cells[0].X;
		int minLocalRow = cells.Min(c => c.Y);
		verticalI.X = -localColumn;
		verticalI.Y = (GameEngine.Rows - 4) - minLocalRow;
		engine.SetCurrentForTest(verticalI);
		engine.LockCurrentForTest();
		engine.CommitClear();
	}

	/// <summary>盤面の指定した矩形範囲を埋めるヘルパー（rowEnd/colEnd は排他）。</summary>
	private static void Fill(GameEngine engine, int rowStart, int rowEnd, int colStart, int colEnd)
	{
		for (int row = rowStart; row < rowEnd; row++)
		{
			for (int x = colStart; x < colEnd; x++)
			{
				engine.Grid[row, x] = TetrominoType.J;
			}
		}
	}

	/// <summary>
	/// 最下行から count 行ちょうどを消去するヘルパー（count は 1〜4）。
	/// 消去対象行を事前に埋めておき、残った隙間をぴったり埋める形のピースで固定する。
	/// <paramref name="avoidPerfectClear"/> が true のときは最上段に残存ブロックを置き、
	/// Perfect Clear の得点テーブルに切り替わらないようにする。
	/// </summary>
	private static void ClearLinesAtBottom(GameEngine engine, int count, bool avoidPerfectClear)
	{
		if (avoidPerfectClear)
		{
			engine.Grid[0, 0] = TetrominoType.J;
		}

		int bottom = GameEngine.Rows;
		Tetromino piece;
		switch (count)
		{
			case 1:
				// 横向き I（1 行 × 4 セル）で列 6..9 を補完する。
				Fill(engine, bottom - 1, bottom, 0, 6);
				piece = new Tetromino(TetrominoType.I) { X = 6, Y = bottom - 2 };
				break;
			case 2:
				// O（2 行 × 2 セル）で列 0..1 を補完する。
				Fill(engine, bottom - 2, bottom, 2, GameEngine.Columns);
				piece = new Tetromino(TetrominoType.O) { X = 0, Y = bottom - 2 };
				break;
			case 3:
				// 縦向き J（列 0 の 3 行 ＋ 最上段の列 1）で隙間を補完する。
				Fill(engine, bottom - 3, bottom - 2, 2, GameEngine.Columns);
				Fill(engine, bottom - 2, bottom, 1, GameEngine.Columns);
				piece = new Tetromino(TetrominoType.J).Rotated();
				piece.X = -1;
				piece.Y = bottom - 3;
				break;
			case 4:
				// 縦向き I（列 0 の 4 行）で補完する。
				Fill(engine, bottom - 4, bottom, 1, GameEngine.Columns);
				piece = new Tetromino(TetrominoType.I).Rotated();
				piece.X = -piece.Blocks().First().X;
				piece.Y = (bottom - 4) - piece.Blocks().Min(c => c.Y);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(count), count, "count は 1〜4 のみ対応する。");
		}

		engine.SetCurrentForTest(piece);
		engine.LockCurrentForTest();
	}

	/// <summary>
	/// 開始直後の状態が初期化されていることを確認する。
	/// パス条件: スコア/ライン 0、レベル 1、ゲームオーバー・消去中でなく、落下ピースが存在する。
	/// </summary>
	[Fact]
	public void StartInitializesCleanState()
	{
		var engine = StartedEngine();

		Assert.Equal(0, engine.Score);
		Assert.Equal(0, engine.Lines);
		Assert.Equal(1, engine.Level);
		Assert.False(engine.IsGameOver);
		Assert.False(engine.IsClearing);
		Assert.NotNull(engine.Current);
	}

	/// <summary>
	/// 開始直後、最初のピースが出現済みのため PieceCount が 1 であることを確認する。
	/// パス条件: <see cref="GameEngine.PieceCount"/> が 1。
	/// </summary>
	[Fact]
	public void StartPieceCountIsOne()
	{
		var engine = StartedEngine();

		Assert.Equal(1, engine.PieceCount);
	}

	/// <summary>
	/// ピース固定で次のピースが出現すると PieceCount が増えることを確認する。
	/// パス条件: ラインを完成させない固定の後、PieceCount が 2 になる。
	/// </summary>
	[Fact]
	public void LockPieceSpawningNextPieceIncrementsPieceCount()
	{
		var engine = StartedEngine();
		// スポーン位置(X=4,Y=0)と重ならない場所に固定し、次ピースが正常に出現できるようにする。
		engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 0, Y = 10 });

		engine.LockCurrentForTest();

		Assert.Equal(2, engine.PieceCount);
	}

	/// <summary>
	/// テトリスを決めると TetrisCount・TotalClearCount がともに 1 増えることを確認する。
	/// パス条件: 4ライン同時消し確定後、TetrisCount==1・TotalClearCount==1。
	/// </summary>
	[Fact]
	public void CommitClearTetrisClearIncrementsTetrisCountAndTotalClearCount()
	{
		var engine = StartedEngine();
		ClearTetrisAtBottom(engine);

		Assert.Equal(1, engine.TetrisCount);
		Assert.Equal(1, engine.TotalClearCount);
	}

	/// <summary>
	/// 通常消去1回・テトリス1回の後、TetrisRate が 50(%) になることを確認する。
	/// パス条件: TotalClearCount==2 のうち TetrisCount==1 なので 50.0。
	/// </summary>
	[Fact]
	public void TetrisRateComputesPercentageOfTetrisClears()
	{
		var engine = StartedEngine();
		ClearSingleLineAtBottom(engine);
		ClearTetrisAtBottom(engine);

		Assert.Equal(50.0, engine.TetrisRate);
	}

	/// <summary>
	/// 開始前は操作しても何も起きないことを確認する。
	/// パス条件: <see cref="GameEngine.MoveLeft"/> が false を返し、落下ピースは null のまま。
	/// </summary>
	[Fact]
	public void MoveLeftBeforeStartDoesNothing()
	{
		var engine = new GameEngine();

		Assert.False(engine.MoveLeft());
		Assert.Null(engine.Current);
	}

	/// <summary>
	/// 右移動後に左移動すると元の列へ戻ることを確認する。
	/// パス条件: 右移動で列 +1、左移動で開始列に戻る（いずれも移動成功）。
	/// </summary>
	[Fact]
	public void MoveRightThenMoveLeftReturnsToSameColumn()
	{
		var engine = StartedEngine();
		int startX = engine.Current!.X;

		Assert.True(engine.MoveRight());
		Assert.Equal(startX + 1, engine.Current!.X);

		Assert.True(engine.MoveLeft());
		Assert.Equal(startX, engine.Current!.X);
	}

	/// <summary>
	/// 左端まで移動するとそれ以上動けないことを確認する。
	/// パス条件: 左端セルが列 0 になり、さらなる左移動は false を返す。
	/// </summary>
	[Fact]
	public void MoveLeftStopsAtLeftWall()
	{
		var engine = StartedEngine();

		// 壁に当たるまで左へ。最終的に左端セルが列 0 になる。
		for (int i = 0; i < GameEngine.Columns; i++)
		{
			engine.MoveLeft();
		}

		int minColumn = engine.Current!.Blocks().Min(b => b.X);
		Assert.Equal(0, minColumn);
		Assert.False(engine.MoveLeft()); // これ以上は動けない
	}

	/// <summary>
	/// 接地位置で SoftDrop しても即座には固定されないことを確認する（ロックディレイ）。
	/// パス条件: 接地させて SoftDrop() を呼んでも Grid には反映されず、Current が残る。
	/// </summary>
	[Fact]
	public void SoftDropWhenGroundedDoesNotLockImmediately()
	{
		var engine = StartedEngine();
		engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 4, Y = GameEngine.Rows - 2 });

		engine.SoftDrop();

		Assert.NotNull(engine.Current);
		Assert.Null(engine.Grid[GameEngine.Rows - 1, 4]);
	}

	/// <summary>
	/// 重力による自然落下（GravityDrop）はプレイヤー操作ではないため加点しないことを確認する。
	/// パス条件: ピースが 1 段下に移動するが、Score は 0 のまま変化しない。
	/// </summary>
	[Fact]
	public void GravityDropMovesDownWithoutAddingScore()
	{
		var engine = StartedEngine();
		engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 4, Y = 0 });

		engine.GravityDrop();

		Assert.Equal(1, engine.Current!.Y);
		Assert.Equal(0, engine.Score);
	}

	/// <summary>
	/// ロックディレイが経過する前は固定されないことを確認する。
	/// パス条件: 500ms 未満の経過時間を渡しても Grid に反映されない。
	/// </summary>
	[Fact]
	public void AdvanceLockDelayBeforeDelayElapsedDoesNotLock()
	{
		var engine = StartedEngine();
		engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 4, Y = GameEngine.Rows - 2 });

		engine.AdvanceLockDelay(TimeSpan.FromMilliseconds(400));

		Assert.Null(engine.Grid[GameEngine.Rows - 1, 4]);
	}

	/// <summary>
	/// ロックディレイが経過すると固定されることを確認する。
	/// パス条件: 500ms 分の経過時間を渡すと Grid にピースが反映される。
	/// </summary>
	[Fact]
	public void AdvanceLockDelayWhileGroundedLocksAfterDelay()
	{
		var engine = StartedEngine();
		engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 4, Y = GameEngine.Rows - 2 });

		engine.AdvanceLockDelay(TimeSpan.FromMilliseconds(500));

		Assert.Equal(TetrominoType.O, engine.Grid[GameEngine.Rows - 1, 4]);
	}

	/// <summary>
	/// ロックディレイ経過時に戻り値で固定発生を通知することを確認する（呼び出し側の再描画判定用）。
	/// パス条件: 500ms 分の経過時間を渡すと戻り値が true になる。
	/// </summary>
	[Fact]
	public void AdvanceLockDelayWhenLockDelayExceededReturnsTrue()
	{
		var engine = StartedEngine();
		engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 4, Y = GameEngine.Rows - 2 });

		bool locked = engine.AdvanceLockDelay(TimeSpan.FromMilliseconds(500));

		Assert.True(locked);
	}

	/// <summary>
	/// 接地していない間はロックディレイの経過時間が積算されないことを確認する。
	/// パス条件: 出現直後（非接地）に十分長い経過時間を渡しても固定されない。
	/// </summary>
	[Fact]
	public void AdvanceLockDelayWhileNotGroundedDoesNotAccumulate()
	{
		var engine = StartedEngine();

		engine.AdvanceLockDelay(TimeSpan.FromSeconds(10));

		Assert.NotNull(engine.Current);
	}

	/// <summary>
	/// 接地中に横移動するとロックディレイがリセットされ、合算しても固定されないことを確認する。
	/// パス条件: 400ms 経過後に横移動でリセットし、さらに 400ms 経過しても固定されない（合計800ms相当だが未固定）。
	/// </summary>
	[Fact]
	public void MoveLeftWhileGroundedResetsLockDelay()
	{
		var engine = StartedEngine();
		engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 4, Y = GameEngine.Rows - 2 });

		engine.AdvanceLockDelay(TimeSpan.FromMilliseconds(400));
		Assert.True(engine.MoveLeft());
		engine.AdvanceLockDelay(TimeSpan.FromMilliseconds(400));

		Assert.Null(engine.Grid[GameEngine.Rows - 1, 3]);
		Assert.NotNull(engine.Current);
	}

	/// <summary>
	/// ロックディレイのリセット回数上限を超えると、移動を続けても最終的に固定されることを確認する。
	/// パス条件: 上限（<see cref="GameEngine.MaxLockResets"/>）を超えて左右移動を繰り返すと Grid に反映される。
	/// </summary>
	[Fact]
	public void LockDelayMaxResetsExceededLocksDespiteContinuedMovement()
	{
		var engine = StartedEngine();
		engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 4, Y = GameEngine.Rows - 2 });

		for (int i = 0; i < GameEngine.MaxLockResets + 5; i++)
		{
			engine.MoveRight();
			engine.MoveLeft();
			engine.AdvanceLockDelay(TimeSpan.FromMilliseconds(100));
		}

		Assert.Equal(TetrominoType.O, engine.Grid[GameEngine.Rows - 1, 4]);
	}

	/// <summary>
	/// 通常の空間で反時計回転が成功し、形状が変化することを確認する。
	/// パス条件: <see cref="GameEngine.RotateCcw"/> が true を返し、Current の Cells が回転前と異なる。
	/// </summary>
	[Fact]
	public void RotateCcwSucceedsWhenSpaceAvailable()
	{
		var engine = StartedEngine();
		engine.SetCurrentForTest(new Tetromino(TetrominoType.T) { X = 4, Y = 4 });
		var before = (bool[,])engine.Current!.Cells.Clone();

		Assert.True(engine.RotateCcw());

		Assert.NotEqual(before, engine.Current!.Cells);
	}

	/// <summary>
	/// 左端に寄せた状態で反時計回転してもウォールキックにより回転が成功することを確認する。
	/// パス条件: 左端で通常なら回転不可な位置から、キック後に回転が成功する。
	/// </summary>
	[Fact]
	public void RotateCcwNearLeftWallUsesWallKick()
	{
		var engine = StartedEngine();

		// 左端まで寄せる。
		for (int i = 0; i < GameEngine.Columns; i++)
		{
			engine.MoveLeft();
		}
		int minColumn = engine.Current!.Blocks().Min(b => b.X);
		Assert.Equal(0, minColumn); // 前提: 左端に到達している

		Assert.True(engine.RotateCcw());

		// 回転後も盤面内に収まっている。
		Assert.True(engine.Current!.Blocks().All(b => b.X >= 0 && b.X < GameEngine.Columns));
	}

	/// <summary>
	/// I ピースの回転で I 専用の大きいキック(dx=-2)が使われ、JLSTZ用テーブルと混同していないことを確認する。
	/// パス条件: オフセット(0,0)は衝突で失敗し、I テーブル固有のオフセット(-2,0)で回転が成功して列5に来る。
	/// </summary>
	[Fact]
	public void RotateIPieceUsesIKickTableNotJlstzTable()
	{
		var engine = StartedEngine();
		engine.SetCurrentForTest(new Tetromino(TetrominoType.I) { X = 5, Y = 8 });
		// オフセット(0,0)での縦棒（列7）をブロックする。JLSTZ用オフセット(-1,0)なら列6に来て回避できてしまうため、
		// I 専用オフセット(-2,0)でのみ回避できる列7への衝突を用意する。
		engine.Grid[10, 7] = TetrominoType.J;

		Assert.True(engine.Rotate());

		Assert.True(engine.Current!.Blocks().All(b => b.X == 5));
	}

	/// <summary>
	/// 開始直後、NextQueue の要素数が PreviewCount(3) であることを確認する。
	/// パス条件: <see cref="GameEngine.NextQueue"/> の Count が 3。
	/// </summary>
	[Fact]
	public void StartNextQueueHasThreePreviewItems()
	{
		var engine = StartedEngine();

		Assert.Equal(3, engine.NextQueue.Count);
	}

	/// <summary>
	/// NextQueue の先頭が NextType と一致することを確認する。
	/// パス条件: <see cref="GameEngine.NextQueue"/>[0] が <see cref="GameEngine.NextType"/> と等しい。
	/// </summary>
	[Fact]
	public void NextQueueFirstItemMatchesNextType()
	{
		var engine = StartedEngine();

		Assert.Equal(engine.NextType, engine.NextQueue[0]);
	}

	/// <summary>
	/// ピース確定後も NextQueue は 3 件を維持し、繰り上がりが正しいことを確認する。
	/// パス条件: 固定前の NextQueue[1] が、固定後の新しい NextType(=NextQueue[0]) と一致する。
	/// </summary>
	[Fact]
	public void SpawnNextConsumesQueueFrontAndRefillsTail()
	{
		var engine = StartedEngine();
		var expectedNewNext = engine.NextQueue[1];

		engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 0, Y = GameEngine.Rows - 2 });
		engine.LockCurrentForTest();

		Assert.Equal(3, engine.NextQueue.Count);
		Assert.Equal(expectedNewNext, engine.NextType);
	}

	/// <summary>
	/// NextQueue 越しに複数個取り出しても 7-bag の制約(1巡に全種1回ずつ)が壊れないことを確認する。
	/// パス条件: 21 回分ピースを進めると、各種がちょうど 3 回ずつ出現する。
	/// </summary>
	[Fact]
	public void NextQueueMaintains7BagFairnessAcrossManySpawns()
	{
		var engine = StartedEngine();
		var counts = new Dictionary<TetrominoType, int>();

		void Count(TetrominoType type)
		{
			counts[type] = counts.GetValueOrDefault(type) + 1;
		}

		Count(engine.Current!.Type);
		for (int i = 0; i < 20; i++)
		{
			engine.SetCurrentForTest(new Tetromino(engine.Current!.Type) { X = 0, Y = GameEngine.Rows - 2 });
			engine.LockCurrentForTest();
			Count(engine.Current!.Type);
		}

		foreach (TetrominoType type in Enum.GetValues<TetrominoType>())
		{
			Assert.Equal(3, counts[type]);
		}
	}

	/// <summary>
	/// ゴースト位置が現在位置以下かつ盤面内に収まることを確認する。
	/// パス条件: GhostY が現在 Y 以上で、ゴースト最下セルが盤面の行数未満。
	/// </summary>
	[Fact]
	public void GhostYIsAtOrBelowCurrentAndWithinBoard()
	{
		var engine = StartedEngine();

		int ghostY = engine.GhostY();

		Assert.True(ghostY >= engine.Current!.Y);
		int maxRow = engine.Current!.Blocks().Max(b => b.Y) + (ghostY - engine.Current!.Y);
		Assert.True(maxRow < GameEngine.Rows);
	}

	/// <summary>
	/// 1 ライン消去が「検出 → 確定」の 2 段階で行われ、確定時に加点されることを確認する。
	/// パス条件: 固定直後は IsClearing=true・Lines/Score=0、CommitClear 後に Lines=1・Score=100、
	/// 消去状態が解除され、上のブロックが最下行へ落ちる。
	/// </summary>
	[Fact]
	public void SingleLineClearIsDetectedThenCommittedWithScore()
	{
		var engine = StartedEngine();

		// 最下行（19）の列 2..9 を事前に埋め、列 0,1 を O ピースで補完して 1 ライン完成させる。
		for (int x = 2; x < GameEngine.Columns; x++)
		{
			engine.Grid[GameEngine.Rows - 1, x] = TetrominoType.I;
		}
		engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 0, Y = GameEngine.Rows - 2 });
		engine.LockCurrentForTest();

		// 確定前: 消去待ち状態で、まだ加点・ライン加算はされない。
		Assert.True(engine.IsClearing);
		Assert.Contains(GameEngine.Rows - 1, engine.PendingClearRows);
		Assert.Equal(0, engine.Lines);
		Assert.Equal(0, engine.Score);

		engine.CommitClear();

		// 確定後: 1 ライン加算、100 点（レベル1）、消去状態は解除。
		Assert.False(engine.IsClearing);
		Assert.Equal(1, engine.Lines);
		Assert.Equal(100, engine.Score);
		// 上にあった O の 2 セルが最下行へ落ちてくる。
		Assert.Equal(TetrominoType.O, engine.Grid[GameEngine.Rows - 1, 0]);
		Assert.Null(engine.Grid[GameEngine.Rows - 1, 2]);
	}

	/// <summary>
	/// 3隅(尖端側2隅とも)が埋まった状態で回転により設置・1ライン消去すると
	/// Full T-Spin Single(800×Level)が加点されることを確認する。
	/// パス条件: 回転成功後にロックし、1ライン消去確定でScore=800。
	/// </summary>
	[Fact]
	public void TSpinFullClearsOneLineScoresTSpinSingle()
	{
		var engine = StartedEngine();

		// 最下行(19)を列5以外すべて埋める(Tの回転後の「下」セルが列5に来て1ライン完成)。
		for (int x = 0; x < GameEngine.Columns; x++)
		{
			if (x != 5)
			{
				engine.Grid[GameEngine.Rows - 1, x] = TetrominoType.J;
			}
		}
		// 行17の列4・列6のみ埋める(回転後の中心(5,18)の上2隅 TL/TR)。
		engine.Grid[GameEngine.Rows - 3, 4] = TetrominoType.J;
		engine.Grid[GameEngine.Rows - 3, 6] = TetrominoType.J;

		// 回転前(spawn姿勢)のTピースをX=4,Y=17に配置し、その場回転(オフセット0,0)で
		// 中心(5,18)・尖端側(右)の2隅(TR,BR)を含む4隅すべてが埋まった状態を作る。
		engine.SetCurrentForTest(new Tetromino(TetrominoType.T) { X = 4, Y = GameEngine.Rows - 3 });
		Assert.True(engine.Rotate());

		engine.LockCurrentForTest();
		Assert.True(engine.IsClearing);
		Assert.Single(engine.PendingClearRows);

		engine.CommitClear();

		Assert.Equal(1, engine.Lines);
		Assert.Equal(800, engine.Score);
	}

	/// <summary>
	/// 3隅(尖端側1隅のみ)が埋まった状態で回転により設置・ライン消去なしの場合、
	/// Mini T-Spinの固定ボーナス(100×Level)が即加算されることを確認する。
	/// パス条件: ラインが完成しない配置でロック後、Score=100。
	/// </summary>
	[Fact]
	public void TSpinMiniNoLinesAddsFlatBonus()
	{
		var engine = StartedEngine();

		// 中心(5,10)の背面2隅(TL,BL)と、尖端側(右)の1隅(TR)のみ埋める。BR は空けておく。
		engine.Grid[9, 4] = TetrominoType.J;  // TL
		engine.Grid[9, 6] = TetrominoType.J;  // TR（尖端側）
		engine.Grid[11, 4] = TetrominoType.J; // BL

		engine.SetCurrentForTest(new Tetromino(TetrominoType.T) { X = 4, Y = 9 });
		Assert.True(engine.Rotate());

		engine.LockCurrentForTest();

		Assert.False(engine.IsClearing);
		Assert.Equal(100, engine.Score);
	}

	/// <summary>
	/// 同じ3隅配置でも、回転を経ずに直接配置した場合は T-Spin と判定されず、
	/// 通常のライン消去点のみになることを確認する。
	/// パス条件: 回転せず直接1ライン消去すると Score が通常の 100(T-Spinなし)になる。
	/// </summary>
	[Fact]
	public void TSpinRequiresLastActionRotationTranslationDoesNotCount()
	{
		var engine = StartedEngine();

		for (int x = 0; x < GameEngine.Columns; x++)
		{
			if (x != 5)
			{
				engine.Grid[GameEngine.Rows - 1, x] = TetrominoType.J;
			}
		}
		engine.Grid[GameEngine.Rows - 3, 4] = TetrominoType.J;
		engine.Grid[GameEngine.Rows - 3, 6] = TetrominoType.J;

		// 回転を経由せず、最終姿勢(state1)を直接配置する(移動のみで到達した扱い)。
		var piece = new Tetromino(TetrominoType.T).Rotated();
		piece.X = 4;
		piece.Y = GameEngine.Rows - 3;
		engine.SetCurrentForTest(piece);

		engine.LockCurrentForTest();
		Assert.True(engine.IsClearing);

		engine.CommitClear();

		Assert.Equal(1, engine.Lines);
		Assert.Equal(100, engine.Score);
	}

	/// <summary>
	/// 単発のライン消去では Combo が 1 になり、コンボボーナスは加算されないことを確認する。
	/// パス条件: 1 回消去後、Combo == 1 かつ Score == 100 * Level（ボーナスなし）。
	/// </summary>
	[Fact]
	public void CommitClearSingleClearComboBecomesOneWithNoBonus()
	{
		var engine = StartedEngine();

		ClearSingleLineAtBottom(engine);

		Assert.Equal(1, engine.Combo);
		Assert.Equal(100 * engine.Level, engine.Score);
	}

	/// <summary>
	/// 消去を伴う固定が連続すると Combo が増え、2 回目の消去にコンボボーナスが加算されることを確認する。
	/// パス条件: 2 回連続消去後、Combo == 2 かつ増加分に 50*(Combo-1)*Level のボーナスが含まれる。
	/// </summary>
	[Fact]
	public void CommitClearConsecutiveClearsComboIncrementsAndAddsBonus()
	{
		var engine = StartedEngine();

		ClearSingleLineAtBottom(engine);
		Assert.Equal(1, engine.Combo);
		int scoreAfterFirst = engine.Score;

		ClearSingleLineAtBottom(engine);

		Assert.Equal(2, engine.Combo);
		int comboBonus = 50 * (engine.Combo - 1) * engine.Level;
		int expectedIncrement = 100 * engine.Level + comboBonus;
		Assert.Equal(scoreAfterFirst + expectedIncrement, engine.Score);
	}

	/// <summary>
	/// ライン消去を伴わない固定が起きると Combo が 0 にリセットされることを確認する。
	/// パス条件: コンボ成立後、消去のない固定で Combo が 0 に戻る。
	/// </summary>
	[Fact]
	public void LockPieceWithoutClearingLinesResetsComboToZero()
	{
		var engine = StartedEngine();
		ClearSingleLineAtBottom(engine);
		Assert.Equal(1, engine.Combo);

		// ラインを完成させない位置にピースを固定する。
		engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 4, Y = 0 });
		engine.LockCurrentForTest();

		Assert.Equal(0, engine.Combo);
	}

	/// <summary>
	/// テトリスに続けてテトリスを決めると Back-to-Back ボーナスが加算されることを確認する。
	/// パス条件: 2 回目のテトリスで IsBackToBack == true になり、基礎点の +50% が加算される。
	/// </summary>
	[Fact]
	public void CommitClearTetrisFollowedByTetrisAddsBackToBackBonus()
	{
		var engine = StartedEngine();

		ClearTetrisAtBottom(engine);
		Assert.False(engine.IsBackToBack);
		int scoreAfterFirst = engine.Score;

		ClearTetrisAtBottom(engine);

		Assert.True(engine.IsBackToBack);
		int baseScore = 800 * engine.Level;
		int comboBonus = 50 * (engine.Combo - 1) * engine.Level;
		int expectedIncrement = baseScore + comboBonus + baseScore / 2;
		Assert.Equal(scoreAfterFirst + expectedIncrement, engine.Score);
	}

	/// <summary>
	/// T-Spin によるライン消去も Back-to-Back の対象（difficult clear）に含まれることを確認する（テトリスガイドライン準拠）。
	/// パス条件: T-Spin Single の直後にテトリスを決めると IsBackToBack == true になる。
	/// </summary>
	[Fact]
	public void CommitClearTSpinFollowedByTetrisAddsBackToBackBonus()
	{
		var engine = StartedEngine();

		// T-Spin Single(1ライン消去)を1回決める。
		for (int x = 0; x < GameEngine.Columns; x++)
		{
			if (x != 5)
			{
				engine.Grid[GameEngine.Rows - 1, x] = TetrominoType.J;
			}
		}
		engine.Grid[GameEngine.Rows - 3, 4] = TetrominoType.J;
		engine.Grid[GameEngine.Rows - 3, 6] = TetrominoType.J;
		engine.SetCurrentForTest(new Tetromino(TetrominoType.T) { X = 4, Y = GameEngine.Rows - 3 });
		Assert.True(engine.Rotate());
		engine.LockCurrentForTest();
		engine.CommitClear();
		Assert.False(engine.IsBackToBack); // 直前に消去がないため初回はB2B対象外

		// 続けてテトリスを決める。
		ClearTetrisAtBottom(engine);

		Assert.True(engine.IsBackToBack);
	}

	/// <summary>
	/// テトリスの間に易しい消去が挟まると Back-to-Back ストリークが途切れることを確認する。
	/// パス条件: テトリス→1ライン消去→テトリスの順で、3 回目のテトリスの IsBackToBack が false。
	/// </summary>
	[Fact]
	public void CommitClearEasyClearBreaksBackToBack()
	{
		var engine = StartedEngine();

		ClearTetrisAtBottom(engine);
		Assert.False(engine.IsBackToBack);

		ClearSingleLineAtBottom(engine);
		Assert.False(engine.IsBackToBack);

		ClearTetrisAtBottom(engine);
		Assert.False(engine.IsBackToBack);
	}

	/// <summary>
	/// ハードドロップでピースがゴースト位置まで落下し固定されることを確認する。
	/// パス条件: HardDrop() 後、ゴースト位置に対応するセルにピースの色が入る。
	/// </summary>
	[Fact]
	public void HardDropLocksPieceAtGhostPosition()
	{
		var engine = StartedEngine();
		engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 4, Y = 0 });
		int ghostY = engine.GhostY();

		engine.HardDrop();

		Assert.Equal(TetrominoType.O, engine.Grid[ghostY + 1, 4]);
		Assert.Equal(TetrominoType.O, engine.Grid[ghostY + 1, 5]);
	}

	/// <summary>
	/// ハードドロップで落下距離×2点が加算されることを確認する。
	/// パス条件: 落下距離(ゴーストYと開始Yの差)の2倍がScoreに加算される。
	/// </summary>
	[Fact]
	public void HardDropAddsDoubleDistanceScore()
	{
		var engine = StartedEngine();
		engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 4, Y = 0 });
		int expectedDistance = engine.GhostY();

		engine.HardDrop();

		Assert.Equal(expectedDistance * 2, engine.Score);
	}

	/// <summary>
	/// 十分な空間がある位置で時計回り回転が成功することを確認する（RotateCcw版の対）。
	/// パス条件: 中央付近で Rotate() が true を返し、形状が変化する。
	/// </summary>
	[Fact]
	public void RotateSucceedsWhenSpaceAvailable()
	{
		var engine = StartedEngine();
		engine.SetCurrentForTest(new Tetromino(TetrominoType.T) { X = 4, Y = 4 });
		var beforeBlocks = engine.Current!.Blocks().ToArray();
		int beforeRotation = engine.Current!.RotationState;

		Assert.True(engine.Rotate());

		Assert.Equal((beforeRotation + 1) % 4, engine.Current!.RotationState);
		Assert.False(beforeBlocks.SequenceEqual(engine.Current!.Blocks().ToArray()));
	}

	/// <summary>
	/// 右端に寄せた縦向き I ピースを時計回転すると、ウォールキックにより横向きへの回転が成功することを確認する
	/// （RotateCcw版の対）。StartedEngine() 直後の Current は 7-bag のランダムなピースに依存し、
	/// 種によってはキックを経由せず回転が成功しうる（例: O ミノ）ため、SetCurrentForTest で
	/// 確実にキックが必須になる配置（1列しか占めない縦Iを右端に置き、4列必要な横向きに回転）を作る。
	/// パス条件: 右端(列9)の縦Iを回転すると RotationState が変化し、盤面内に収まる。
	/// </summary>
	[Fact]
	public void RotateNearRightWallUsesWallKick()
	{
		var engine = StartedEngine();
		var verticalI = new Tetromino(TetrominoType.I).Rotated(); // 状態1(縦向き、1列のみ占有)
		engine.SetCurrentForTest(verticalI);

		// 右端まで寄せる。
		while (engine.MoveRight())
		{
		}
		int maxColumn = engine.Current!.Blocks().Max(b => b.X);
		Assert.Equal(GameEngine.Columns - 1, maxColumn); // 前提: 右端(列9)に到達している
		int beforeRotation = engine.Current!.RotationState;

		Assert.True(engine.Rotate()); // 横向き(4列必要)への回転はキック無しでは列9に収まらない

		Assert.Equal((beforeRotation + 1) % 4, engine.Current!.RotationState);
		Assert.True(engine.Current!.Blocks().All(b => b.X >= 0 && b.X < GameEngine.Columns));
	}

	/// <summary>
	/// 次のピースがスポーンできない状態で固定すると IsGameOver が true になることを確認する。
	/// パス条件: スポーン位置(X=3〜6, 行0-2)の列を塞いだ状態でライン消去を伴わない固定をすると、
	/// IsGameOver=true かつ Current=null になる。
	/// </summary>
	[Fact]
	public void LockPieceWhenSpawnBlockedSetsGameOver()
	{
		var engine = StartedEngine();
		for (int y = 0; y < 3; y++)
		{
			for (int x = 3; x <= 6; x++)
			{
				engine.Grid[y, x] = TetrominoType.J;
			}
		}
		engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 0, Y = 10 });

		engine.LockCurrentForTest();

		Assert.True(engine.IsGameOver);
		Assert.Null(engine.Current);
	}

	/// <summary>
	/// ピースが固定されると PieceLocked イベントが発火することを確認する。
	/// パス条件: LockCurrentForTest() 呼び出しで PieceLocked が発火する。
	/// </summary>
	[Fact]
	public void LockPieceRaisesPieceLockedEvent()
	{
		var engine = StartedEngine();
		engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 0, Y = 10 });
		bool raised = false;
		engine.PieceLocked += (_, _) => raised = true;

		engine.LockCurrentForTest();

		Assert.True(raised);
	}

	/// <summary>
	/// ソフトドロップで1セル下がるごとに1点加算されることを確認する。
	/// パス条件: 上部の空きスペースで SoftDrop() を2回呼ぶと Score=2。
	/// </summary>
	[Fact]
	public void SoftDropMovesDownAddsOnePointPerCell()
	{
		var engine = StartedEngine();
		engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 4, Y = 0 });

		engine.SoftDrop();
		engine.SoftDrop();

		Assert.Equal(2, engine.Score);
	}

	/// <summary>
	/// ライン消去の確定待ち(IsClearing)の間は、固定中のピースが無い(Current=null)ため
	/// 移動操作が抑止されることを確認する。
	/// パス条件: 満杯行を固定して IsClearing になった状態で MoveLeft() を呼んでも false が返る。
	/// </summary>
	[Fact]
	public void MoveLeftWhileClearingDoesNothing()
	{
		var engine = StartedEngine();
		for (int x = 0; x < GameEngine.Columns; x++)
		{
			engine.Grid[GameEngine.Rows - 1, x] = TetrominoType.J;
		}
		engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 0, Y = 10 });
		engine.LockCurrentForTest();
		Assert.True(engine.IsClearing);
		Assert.Null(engine.Current);

		bool moved = engine.MoveLeft();

		Assert.False(moved);
	}

	/// <summary>
	/// ライン消去数に応じた通常の得点テーブル（1/2/3/4 = 100/300/500/800、レベル補正あり）を確認する。
	/// パス条件: count 行ちょうど消去して確定すると Score が期待値 × Level になる。
	/// </summary>
	[Theory]
	[InlineData(1, 100)]
	[InlineData(2, 300)]
	[InlineData(3, 500)]
	[InlineData(4, 800)]
	public void CommitClearScoresFollowLineCountTable(int lineCount, int expectedBaseScore)
	{
		var engine = StartedEngine();
		ClearLinesAtBottom(engine, lineCount, avoidPerfectClear: true);
		Assert.Equal(lineCount, engine.PendingClearRows.Count);

		engine.CommitClear();

		Assert.Equal(lineCount, engine.Lines);
		Assert.Equal(expectedBaseScore * engine.Level, engine.Score);
	}

	/// <summary>
	/// Perfect Clear（全消し）時の得点テーブル（1/2/3/4 = 800/1200/1800/2000、レベル補正あり）を確認する。
	/// パス条件: 消去後に盤面が空になる形で count 行消去すると、通常テーブルではなく Perfect Clear の得点になる。
	/// </summary>
	[Theory]
	[InlineData(1, 800)]
	[InlineData(2, 1200)]
	[InlineData(3, 1800)]
	[InlineData(4, 2000)]
	public void CommitClearPerfectClearScoresFollowPerfectClearTable(int lineCount, int expectedBaseScore)
	{
		var engine = StartedEngine();
		ClearLinesAtBottom(engine, lineCount, avoidPerfectClear: false);
		Assert.Equal(lineCount, engine.PendingClearRows.Count);

		engine.CommitClear();

		Assert.Equal(lineCount, engine.Lines);
		Assert.Equal(expectedBaseScore * engine.Level, engine.Score);
	}

	/// <summary>
	/// 消去ライン数に応じてレベルが上がり、落下間隔が短くなることを確認する。
	/// 下限 80ms へのクランプ（<c>Math.Max(80, 800 - (Level - 1) * 70)</c>）を跨ぐ境界値を含む。
	/// パス条件: 1 ライン消去を clears 回行うと Lines/Level/DropInterval が期待値になる。
	/// </summary>
	[Theory]
	[InlineData(0, 1, 800)]
	[InlineData(10, 2, 730)]
	[InlineData(50, 6, 450)]
	[InlineData(100, 11, 100)]
	[InlineData(110, 12, 80)]  // 計算上は 30ms だが下限 80ms にクランプされる
	[InlineData(150, 16, 80)]
	public void LevelAndDropIntervalFollowClearedLineCount(int clears, int expectedLevel, int expectedIntervalMs)
	{
		var engine = StartedEngine();
		for (int i = 0; i < clears; i++)
		{
			ClearSingleLineAtBottom(engine);
		}

		Assert.Equal(clears, engine.Lines);
		Assert.Equal(expectedLevel, engine.Level);
		Assert.Equal(TimeSpan.FromMilliseconds(expectedIntervalMs), engine.DropInterval);
	}


	/// <summary>
	/// T-Spin 判定の「前方/後方」の隅を回転姿勢ごとに返す（<c>DetectTSpin</c> の switch と対応）。
	/// オフセットは中心セルからの相対位置。
	/// </summary>
	private static ((int Dx, int Dy)[] Front, (int Dx, int Dy)[] Back) TSpinCorners(int rotationState)
	{
		(int, int) tl = (-1, -1), tr = (1, -1), bl = (-1, 1), br = (1, 1);
		return rotationState switch
		{
			0 => (new[] { tl, tr }, new[] { bl, br }), // 尖端: 上
			1 => (new[] { tr, br }, new[] { tl, bl }), // 尖端: 右
			2 => (new[] { bl, br }, new[] { tl, tr }), // 尖端: 下
			_ => (new[] { tl, bl }, new[] { tr, br }), // 尖端: 左
		};
	}

	/// <summary>
	/// T ピースを指定の回転姿勢へ「実際に回転させて」到達させ、指定数の隅を埋めた盤面を用意する。
	/// DetectTSpin は直前の成功アクションが回転であることを要求するため、SetCurrentForTest で
	/// 1 つ手前の姿勢を置いてから Rotate() を呼ぶ必要がある。
	/// </summary>
	private static GameEngine ArrangeTSpin(int toState, int frontFilled, int backFilled, int centerX = 4, int centerY = 10)
	{
		var engine = StartedEngine();
		var (front, back) = TSpinCorners(toState);
		for (int i = 0; i < frontFilled; i++)
		{
			engine.Grid[centerY + front[i].Dy, centerX + front[i].Dx] = TetrominoType.J;
		}
		for (int i = 0; i < backFilled; i++)
		{
			engine.Grid[centerY + back[i].Dy, centerX + back[i].Dx] = TetrominoType.J;
		}

		// 1 つ手前の姿勢で置いてから回転させ、目的の姿勢に到達させる。
		var piece = new Tetromino(TetrominoType.T);
		for (int i = 0; i < (toState + 3) % 4; i++)
		{
			piece = piece.Rotated();
		}
		piece.X = centerX - 1;
		piece.Y = centerY - 1;
		engine.SetCurrentForTest(piece);

		Assert.True(engine.Rotate(), $"回転に失敗した (toState={toState})");
		Assert.Equal(toState, engine.Current!.RotationState);
		return engine;
	}

	/// <summary>
	/// 4 つの回転姿勢すべてで T-Spin Full が判定され、ライン消去を伴わない固定で 400×Level が入ることを確認する。
	/// パス条件: 前方 2 隅 + 後方 1 隅を埋めて回転・固定すると Score が 400×Level になる。
	/// </summary>
	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public void DetectTSpinFullInEveryRotationState(int toState)
	{
		var engine = ArrangeTSpin(toState, frontFilled: 2, backFilled: 1);

		engine.LockCurrentForTest();

		Assert.Empty(engine.PendingClearRows);
		Assert.Equal(400 * engine.Level, engine.Score);
	}

	/// <summary>
	/// 4 つの回転姿勢すべてで T-Spin Mini が判定され、ライン消去を伴わない固定で 100×Level が入ることを確認する。
	/// パス条件: 前方 1 隅 + 後方 2 隅を埋めて回転・固定すると Score が 100×Level になる。
	/// </summary>
	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public void DetectTSpinMiniInEveryRotationState(int toState)
	{
		var engine = ArrangeTSpin(toState, frontFilled: 1, backFilled: 2);

		engine.LockCurrentForTest();

		Assert.Empty(engine.PendingClearRows);
		Assert.Equal(100 * engine.Level, engine.Score);
	}

	/// <summary>
	/// 埋まっている隅が 3 つ未満のときは T-Spin と判定されないことを確認する。
	/// パス条件: 前方 1 隅 + 後方 1 隅（計 2 隅）では固定点ボーナスが入らない。
	/// </summary>
	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public void DetectTSpinNoneWhenFewerThanThreeCornersOccupied(int toState)
	{
		var engine = ArrangeTSpin(toState, frontFilled: 1, backFilled: 1);

		engine.LockCurrentForTest();

		Assert.Empty(engine.PendingClearRows);
		Assert.Equal(0, engine.Score);
	}

	/// <summary>
	/// T-Spin Mini で 1 ライン消去したときに専用の得点(200×Level)が入ることを確認する。
	/// パス条件: 尖端上向きの T が横一列を完成させ、前方 1 隅・後方 2 隅が埋まっていると Score=200×Level。
	/// </summary>
	[Fact]
	public void CommitClearTSpinMiniSingleScoresMiniTable()
	{
		const int centerX = 4;
		const int centerY = GameEngine.Rows - 2;
		var engine = StartedEngine();

		// 尖端上向き(state 0)の T は中心行に left/center/right の 3 セルを持つ。
		// その行の残り 7 セルを埋めておき、固定で 1 ライン完成させる。
		for (int x = 0; x < GameEngine.Columns; x++)
		{
			if (x < centerX - 1 || x > centerX + 1)
			{
				engine.Grid[centerY, x] = TetrominoType.J;
			}
		}
		// 後方(下)の 2 隅と前方(上)の 1 隅だけを埋めて Mini 条件にする。
		engine.Grid[centerY + 1, centerX - 1] = TetrominoType.J;
		engine.Grid[centerY + 1, centerX + 1] = TetrominoType.J;
		engine.Grid[centerY - 1, centerX - 1] = TetrominoType.J;

		// state 3 で置いてから回転して state 0 にする（直前アクションを回転にするため）。
		var piece = new Tetromino(TetrominoType.T).Rotated().Rotated().Rotated();
		piece.X = centerX - 1;
		piece.Y = centerY - 1;
		engine.SetCurrentForTest(piece);
		Assert.True(engine.Rotate());
		Assert.Equal(0, engine.Current!.RotationState);

		engine.LockCurrentForTest();
		Assert.Single(engine.PendingClearRows);

		engine.CommitClear();

		Assert.Equal(1, engine.Lines);
		Assert.Equal(200 * engine.Level, engine.Score);
	}
}
