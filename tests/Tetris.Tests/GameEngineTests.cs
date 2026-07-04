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
}
