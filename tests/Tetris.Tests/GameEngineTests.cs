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

    /// <summary>最下 4 行をテトリス(4ライン同時消し)で完成させて確定するヘルパー（コンボ・B2B テスト用）。</summary>
    private static void ClearTetrisAtBottom(GameEngine engine)
    {
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
}
