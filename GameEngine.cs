namespace Tetris;

/// <summary>
/// テトリスの盤面とゲーム進行を管理するエンジン。描画・入力からは独立している。
/// </summary>
public sealed class GameEngine
{
    public const int Columns = 10;
    public const int Rows = 20;

    private readonly Random _random = new();
    private readonly Queue<TetrominoType> _bag = new();

    /// <summary>固定済みブロックの色。null は空セル。</summary>
    public TetrominoType?[,] Grid { get; } = new TetrominoType?[Rows, Columns];

    public Tetromino? Current { get; private set; }
    public TetrominoType NextType { get; private set; }

    public int Score { get; private set; }
    public int Lines { get; private set; }
    public int Level => Lines / 10 + 1;
    public bool IsGameOver { get; private set; }

    /// <summary>現在のレベルに応じた 1 ステップの落下間隔。</summary>
    public TimeSpan DropInterval => TimeSpan.FromMilliseconds(Math.Max(80, 800 - (Level - 1) * 70));

    public void Start()
    {
        Array.Clear(Grid, 0, Grid.Length);
        _bag.Clear();
        Score = 0;
        Lines = 0;
        IsGameOver = false;
        NextType = NextFromBag();
        SpawnNext();
    }

    /// <summary>7-bag 方式で次のピース種別を取り出す。</summary>
    private TetrominoType NextFromBag()
    {
        if (_bag.Count == 0)
        {
            var types = Enum.GetValues<TetrominoType>().OrderBy(_ => _random.Next()).ToArray();
            foreach (var t in types)
            {
                _bag.Enqueue(t);
            }
        }
        return _bag.Dequeue();
    }

    private void SpawnNext()
    {
        var piece = new Tetromino(NextType)
        {
            X = (Columns - 1) / 2 - 1,
            Y = 0,
        };
        NextType = NextFromBag();

        if (!IsValid(piece))
        {
            IsGameOver = true;
            Current = null;
            return;
        }
        Current = piece;
    }

    public bool MoveLeft() => TryMove(-1, 0);
    public bool MoveRight() => TryMove(1, 0);

    /// <summary>1 段落下を試みる。着地したら固定処理を行う。</summary>
    public void SoftDrop()
    {
        if (IsGameOver || Current is null)
        {
            return;
        }
        if (!TryMove(0, 1))
        {
            LockPiece();
        }
        else
        {
            Score += 1; // ソフトドロップのボーナス
        }
    }

    /// <summary>一気に落下させて固定する。</summary>
    public void HardDrop()
    {
        if (IsGameOver || Current is null)
        {
            return;
        }
        int distance = 0;
        while (TryMove(0, 1))
        {
            distance++;
        }
        Score += distance * 2;
        LockPiece();
    }

    public bool Rotate()
    {
        if (IsGameOver || Current is null)
        {
            return false;
        }
        var rotated = Current.Rotated();
        // 簡易ウォールキック: その場 → 右 → 左 → 上 の順に試す。
        foreach (int dx in new[] { 0, 1, -1, 2, -2 })
        {
            var test = rotated.Clone();
            test.X += dx;
            if (IsValid(test))
            {
                Current = test;
                return true;
            }
        }
        return false;
    }

    private bool TryMove(int dx, int dy)
    {
        if (IsGameOver || Current is null)
        {
            return false;
        }
        var test = Current.Clone();
        test.X += dx;
        test.Y += dy;
        if (IsValid(test))
        {
            Current = test;
            return true;
        }
        return false;
    }

    private bool IsValid(Tetromino piece)
    {
        foreach (var (x, y) in piece.Blocks())
        {
            if (x < 0 || x >= Columns || y < 0 || y >= Rows)
            {
                return false;
            }
            if (Grid[y, x] is not null)
            {
                return false;
            }
        }
        return true;
    }

    private void LockPiece()
    {
        if (Current is null)
        {
            return;
        }
        foreach (var (x, y) in Current.Blocks())
        {
            if (y >= 0 && y < Rows && x >= 0 && x < Columns)
            {
                Grid[y, x] = Current.Type;
            }
        }
        ClearLines();
        SpawnNext();
    }

    private void ClearLines()
    {
        int cleared = 0;
        for (int y = Rows - 1; y >= 0; y--)
        {
            bool full = true;
            for (int x = 0; x < Columns; x++)
            {
                if (Grid[y, x] is null)
                {
                    full = false;
                    break;
                }
            }

            if (full)
            {
                // 上の行を 1 つずつ下にずらす。
                for (int row = y; row > 0; row--)
                {
                    for (int x = 0; x < Columns; x++)
                    {
                        Grid[row, x] = Grid[row - 1, x];
                    }
                }
                for (int x = 0; x < Columns; x++)
                {
                    Grid[0, x] = null;
                }
                cleared++;
                y++; // 同じ行をもう一度判定する。
            }
        }

        if (cleared > 0)
        {
            Lines += cleared;
            // 消したライン数に応じた得点（レベル補正あり）。
            int[] table = { 0, 100, 300, 500, 800 };
            Score += table[cleared] * Level;
        }
    }

    /// <summary>ハードドロップ時のゴースト（着地予測）位置の Y を返す。</summary>
    public int GhostY()
    {
        if (Current is null)
        {
            return 0;
        }
        var ghost = Current.Clone();
        while (true)
        {
            var next = ghost.Clone();
            next.Y += 1;
            if (IsValid(next))
            {
                ghost = next;
            }
            else
            {
                break;
            }
        }
        return ghost.Y;
    }
}
