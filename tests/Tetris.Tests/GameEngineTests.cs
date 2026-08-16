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

    /// <summary>
    /// 開始直後の状態が初期化されていることを確認する。
    /// パス条件: スコア/ライン 0、レベル 1、ゲームオーバー・消去中でなく、落下ピースが存在する。
    /// </summary>
    [Fact]
    public void Start_InitializesCleanState()
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
    public void Start_PieceCount_IsOne()
    {
        var engine = StartedEngine();

        Assert.Equal(1, engine.PieceCount);
    }

    /// <summary>
    /// ピース固定で次のピースが出現すると PieceCount が増えることを確認する。
    /// パス条件: ラインを完成させない固定の後、PieceCount が 2 になる。
    /// </summary>
    [Fact]
    public void LockPiece_SpawningNextPiece_IncrementsPieceCount()
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
    public void CommitClear_TetrisClear_IncrementsTetrisCountAndTotalClearCount()
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
    public void TetrisRate_ComputesPercentageOfTetrisClears()
    {
        var engine = StartedEngine();
        ClearSingleLineAtBottom(engine);
        ClearTetrisAtBottom(engine);

        Assert.Equal(50.0, engine.TetrisRate);
    }

    /// <summary>
    /// レベル 1 の落下間隔が 800ms であることを確認する。
    /// パス条件: <see cref="GameEngine.DropInterval"/> が 800ms。
    /// </summary>
    [Fact]
    public void DropInterval_AtLevelOne_Is800ms()
    {
        var engine = StartedEngine();

        Assert.Equal(System.TimeSpan.FromMilliseconds(800), engine.DropInterval);
    }

    /// <summary>
    /// 開始前は操作しても何も起きないことを確認する。
    /// パス条件: <see cref="GameEngine.MoveLeft"/> が false を返し、落下ピースは null のまま。
    /// </summary>
    [Fact]
    public void MoveLeft_BeforeStart_DoesNothing()
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
    public void MoveRight_ThenMoveLeft_ReturnsToSameColumn()
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
    public void MoveLeft_StopsAtLeftWall()
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
    public void SoftDrop_WhenGrounded_DoesNotLockImmediately()
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
    public void GravityDrop_MovesDownWithoutAddingScore()
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
    public void AdvanceLockDelay_BeforeDelayElapsed_DoesNotLock()
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
    public void AdvanceLockDelay_WhileGrounded_LocksAfterDelay()
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
    public void AdvanceLockDelay_WhenLockDelayExceeded_ReturnsTrue()
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
    public void AdvanceLockDelay_WhileNotGrounded_DoesNotAccumulate()
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
    public void MoveLeft_WhileGrounded_ResetsLockDelay()
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
    public void LockDelay_MaxResetsExceeded_LocksDespiteContinuedMovement()
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
    public void RotateCcw_Succeeds_WhenSpaceAvailable()
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
    public void RotateCcw_NearLeftWall_UsesWallKick()
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
    public void Rotate_IPiece_UsesIKickTable_NotJlstzTable()
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
    public void Start_NextQueue_HasThreePreviewItems()
    {
        var engine = StartedEngine();

        Assert.Equal(3, engine.NextQueue.Count);
    }

    /// <summary>
    /// NextQueue の先頭が NextType と一致することを確認する。
    /// パス条件: <see cref="GameEngine.NextQueue"/>[0] が <see cref="GameEngine.NextType"/> と等しい。
    /// </summary>
    [Fact]
    public void NextQueue_FirstItem_MatchesNextType()
    {
        var engine = StartedEngine();

        Assert.Equal(engine.NextType, engine.NextQueue[0]);
    }

    /// <summary>
    /// ピース確定後も NextQueue は 3 件を維持し、繰り上がりが正しいことを確認する。
    /// パス条件: 固定前の NextQueue[1] が、固定後の新しい NextType(=NextQueue[0]) と一致する。
    /// </summary>
    [Fact]
    public void SpawnNext_ConsumesQueueFrontAndRefillsTail()
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
    public void NextQueue_Maintains7BagFairnessAcrossManySpawns()
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
    public void GhostY_IsAtOrBelowCurrentAndWithinBoard()
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
    public void SingleLineClear_IsDetectedThenCommittedWithScore()
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
    /// 4 ライン同時消し（テトリス）で 800 点が入ることを確認する。
    /// パス条件: 縦 I で 4 行を完成させ確定すると Lines=4・Score=800・消去状態が解除される。
    /// </summary>
    [Fact]
    public void TetrisClear_FourLines_Scores800()
    {
        var engine = StartedEngine();

        // 最上段に残存ブロックを置き、Perfect Clear（全消し）にならないようにする（本テストは通常のテトリス得点を検証する）。
        engine.Grid[0, 0] = TetrominoType.J;

        // 下 4 行の列 1..9 を埋め、列 0 を縦 I ピースで補完して 4 ライン同時消し。
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
        verticalI.X = -localColumn;                       // 縦 I を列 0 へ
        verticalI.Y = (GameEngine.Rows - 4) - minLocalRow; // 行 16..19 を覆う
        engine.SetCurrentForTest(verticalI);
        engine.LockCurrentForTest();

        Assert.True(engine.IsClearing);
        Assert.Equal(4, engine.PendingClearRows.Count);

        engine.CommitClear();

        Assert.Equal(4, engine.Lines);
        Assert.Equal(800, engine.Score);
        Assert.False(engine.IsClearing);
    }

    /// <summary>
    /// 3隅(尖端側2隅とも)が埋まった状態で回転により設置・1ライン消去すると
    /// Full T-Spin Single(800×Level)が加点されることを確認する。
    /// パス条件: 回転成功後にロックし、1ライン消去確定でScore=800。
    /// </summary>
    [Fact]
    public void TSpin_Full_ClearsOneLine_ScoresTSpinSingle()
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
    public void TSpin_Mini_NoLines_AddsFlatBonus()
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
    public void TSpin_RequiresLastActionRotation_TranslationDoesNotCount()
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
    public void CommitClear_SingleClear_ComboBecomesOneWithNoBonus()
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
    public void CommitClear_ConsecutiveClears_ComboIncrementsAndAddsBonus()
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
    public void LockPiece_WithoutClearingLines_ResetsComboToZero()
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
    public void CommitClear_TetrisFollowedByTetris_AddsBackToBackBonus()
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
    public void CommitClear_TSpinFollowedByTetris_AddsBackToBackBonus()
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
    public void CommitClear_EasyClearBreaksBackToBack()
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
    /// 1ライン消去で盤面が完全に空になる（Perfect Clear）と、通常の得点(100×Level)ではなく
    /// Perfect Clearボーナス(800×Level)が加算されることを確認する。
    /// パス条件: 最下行の列0-5をJで、列6-9を横向きIピースで埋めて1ライン消去・全消しにすると Score=800×Level。
    /// </summary>
    [Fact]
    public void CommitClear_PerfectClearSingleLine_ScoresPerfectClearBonus()
    {
        var engine = StartedEngine();

        for (int x = 0; x < 6; x++)
        {
            engine.Grid[GameEngine.Rows - 1, x] = TetrominoType.J;
        }
        var piece = new Tetromino(TetrominoType.I) { X = 6, Y = GameEngine.Rows - 2 };
        engine.SetCurrentForTest(piece);
        engine.LockCurrentForTest();
        Assert.True(engine.IsClearing);

        engine.CommitClear();

        Assert.Equal(1, engine.Lines);
        Assert.Equal(800 * engine.Level, engine.Score);
    }

    /// <summary>
    /// テトリス（4ライン同時消し）で盤面が完全に空になる（Perfect Clear）と、通常のテトリス得点(800×Level)
    /// ではなく Perfect Clearボーナス(2000×Level)が加算されることを確認する。
    /// パス条件: 下4行の列1-9をJで、列0を縦Iピースで埋めて4ライン同時消し・全消しにすると Score=2000×Level。
    /// </summary>
    [Fact]
    public void CommitClear_PerfectClearTetris_ScoresPerfectClearBonus()
    {
        var engine = StartedEngine();

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
        Assert.True(engine.IsClearing);

        engine.CommitClear();

        Assert.Equal(4, engine.Lines);
        Assert.Equal(2000 * engine.Level, engine.Score);
    }

    /// <summary>
    /// ハードドロップでピースがゴースト位置まで落下し固定されることを確認する。
    /// パス条件: HardDrop() 後、ゴースト位置に対応するセルにピースの色が入る。
    /// </summary>
    [Fact]
    public void HardDrop_LocksPieceAtGhostPosition()
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
    public void HardDrop_AddsDoubleDistanceScore()
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
    public void Rotate_Succeeds_WhenSpaceAvailable()
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
    public void Rotate_NearRightWall_UsesWallKick()
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
    public void LockPiece_WhenSpawnBlocked_SetsGameOver()
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
    public void LockPiece_RaisesPieceLockedEvent()
    {
        var engine = StartedEngine();
        engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 0, Y = 10 });
        bool raised = false;
        engine.PieceLocked += (_, _) => raised = true;

        engine.LockCurrentForTest();

        Assert.True(raised);
    }

    /// <summary>
    /// 10ライン消去するとレベルが2に上がり、DropIntervalも短縮されることを確認する。
    /// パス条件: 1ライン消去を10回行うと Lines=10・Level=2・DropInterval=730ms(800-(2-1)*70)。
    /// </summary>
    [Fact]
    public void Level_AfterTenLines_BecomesTwo()
    {
        var engine = StartedEngine();
        for (int i = 0; i < 10; i++)
        {
            ClearSingleLineAtBottom(engine);
        }

        Assert.Equal(10, engine.Lines);
        Assert.Equal(2, engine.Level);
        Assert.Equal(TimeSpan.FromMilliseconds(730), engine.DropInterval);
    }

    /// <summary>
    /// 十分高いレベルでは DropInterval が下限の80msに張り付くことを確認する。
    /// パス条件: テトリス(4ライン)消去を28回行うと Lines=112・Level=12、
    /// 計算上は800-(12-1)*70=30msだが下限80msにクランプされる。
    /// </summary>
    [Fact]
    public void DropInterval_AtHighLevel_ClampsToEightyMs()
    {
        var engine = StartedEngine();
        for (int i = 0; i < 28; i++)
        {
            ClearTetrisAtBottom(engine);
        }

        Assert.Equal(112, engine.Lines);
        Assert.Equal(12, engine.Level);
        Assert.Equal(TimeSpan.FromMilliseconds(80), engine.DropInterval);
    }

    /// <summary>
    /// ソフトドロップで1セル下がるごとに1点加算されることを確認する。
    /// パス条件: 上部の空きスペースで SoftDrop() を2回呼ぶと Score=2。
    /// </summary>
    [Fact]
    public void SoftDrop_MovesDown_AddsOnePointPerCell()
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
    public void MoveLeft_WhileClearing_DoesNothing()
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
}
