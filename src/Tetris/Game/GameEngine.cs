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
    private readonly List<int> _pendingClear = new();

    /// <summary>接地してから固定するまでの猶予時間。</summary>
    private static readonly TimeSpan LockDelayDuration = TimeSpan.FromMilliseconds(500);

    /// <summary>ロックディレイをリセットできる最大回数（無限の設置回避のため）。</summary>
    public const int MaxLockResets = 15;

    private TimeSpan _lockDelayElapsed = TimeSpan.Zero;
    private int _lockResetCount;

    /// <summary>固定済みブロックの色。null は空セル。</summary>
    public TetrominoType?[,] Grid { get; } = new TetrominoType?[Rows, Columns];

    public Tetromino? Current { get; private set; }
    public TetrominoType NextType { get; private set; }

    /// <summary>ホールド（保管）中のピース種。まだ何も保管していない場合は null。</summary>
    public TetrominoType? HeldType { get; private set; }

    /// <summary>現在のピースをホールドできるか（1 ピースにつき設置まで 1 回）。</summary>
    public bool CanHold { get; private set; }

    /// <summary>消去待ち（アニメーション中）の行番号。<see cref="CommitClear"/> で実際に消える。</summary>
    public IReadOnlyList<int> PendingClearRows => _pendingClear;

    /// <summary>満杯行の消去アニメーション待ちかどうか。</summary>
    public bool IsClearing => _pendingClear.Count > 0;

    public int Score { get; private set; }
    public int Lines { get; private set; }
    public int Level => Lines / 10 + 1;
    public bool IsGameOver { get; private set; }

    /// <summary>ピースが盤面に固定された直後に発火する（効果音などのトリガー用）。</summary>
    public event EventHandler? PieceLocked;

    /// <summary>現在のレベルに応じた 1 ステップの落下間隔。</summary>
    public TimeSpan DropInterval => TimeSpan.FromMilliseconds(Math.Max(80, 800 - (Level - 1) * 70));

    /// <summary>現在のピースがこれ以上下に動けない（接地している）かどうか。</summary>
    public bool IsGrounded => Current is not null && !CanMoveDown(Current);

    private bool CanMoveDown(Tetromino piece)
    {
        var test = piece.Clone();
        test.Y += 1;
        return IsValid(test);
    }

    public void Start()
    {
        Array.Clear(Grid, 0, Grid.Length);
        _bag.Clear();
        _pendingClear.Clear();
        Score = 0;
        Lines = 0;
        IsGameOver = false;
        HeldType = null;
        CanHold = true;
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
        var type = NextType;
        NextType = NextFromBag();
        SpawnPiece(type);
    }

    /// <summary>指定種のピースを出現位置に生成する。置けなければゲームオーバー。</summary>
    private void SpawnPiece(TetrominoType type)
    {
        var piece = new Tetromino(type)
        {
            X = (Columns - 1) / 2 - 1,
            Y = 0,
        };

        if (!IsValid(piece))
        {
            IsGameOver = true;
            Current = null;
            return;
        }
        Current = piece;
        _lockDelayElapsed = TimeSpan.Zero;
        _lockResetCount = 0;
    }

    /// <summary>
    /// 現在のピースをホールドする。保管が空ならNEXTを出し、あれば保管ピースと入れ替える。
    /// 1 ピースにつき設置するまで 1 回だけ可能。
    /// </summary>
    public void Hold()
    {
        if (IsGameOver || IsClearing || Current is null || !CanHold)
        {
            return;
        }

        var currentType = Current.Type;
        if (HeldType is null)
        {
            HeldType = currentType;
            SpawnNext();
        }
        else
        {
            var swap = HeldType.Value;
            HeldType = currentType;
            SpawnPiece(swap);
        }
        CanHold = false;
    }

    public bool MoveLeft() => TryMove(-1, 0);
    public bool MoveRight() => TryMove(1, 0);

    /// <summary>1 段落下を試みる。接地している場合はロックディレイ猶予中として何もしない。</summary>
    public void SoftDrop()
    {
        if (IsGameOver || Current is null)
        {
            return;
        }
        if (TryMove(0, 1))
        {
            Score += 1; // ソフトドロップのボーナス
        }
    }

    /// <summary>接地からの経過時間を進める。ロックディレイを超えたら固定する。非接地なら経過時間をリセットする。</summary>
    public void AdvanceLockDelay(TimeSpan elapsed)
    {
        if (IsGameOver || Current is null || !IsGrounded)
        {
            _lockDelayElapsed = TimeSpan.Zero;
            return;
        }
        _lockDelayElapsed += elapsed;
        if (_lockDelayElapsed >= LockDelayDuration)
        {
            LockPiece();
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
                OnSuccessfulAction();
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
            OnSuccessfulAction();
            return true;
        }
        return false;
    }

    /// <summary>移動・回転が成功した際に呼ぶ。接地中ならロックディレイをリセットする（上限あり）。</summary>
    private void OnSuccessfulAction()
    {
        if (!IsGrounded)
        {
            _lockDelayElapsed = TimeSpan.Zero;
            _lockResetCount = 0;
            return;
        }
        if (_lockResetCount < MaxLockResets)
        {
            _lockDelayElapsed = TimeSpan.Zero;
            _lockResetCount++;
        }
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
        Current = null;
        CanHold = true; // 次のピースは再びホールド可能。
        PieceLocked?.Invoke(this, EventArgs.Empty);

        // 満杯行を検出して保留する（実際の消去は CommitClear まで遅延し、アニメを見せる）。
        for (int y = 0; y < Rows; y++)
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
                _pendingClear.Add(y);
            }
        }

        // 消去待ちが無ければそのまま次のピースへ。あれば呼び出し側の CommitClear を待つ。
        if (_pendingClear.Count == 0)
        {
            SpawnNext();
        }
    }

    /// <summary>保留中の満杯行を実際に消去し、下詰め・加点を行って次のピースを生成する。</summary>
    public void CommitClear()
    {
        if (_pendingClear.Count == 0)
        {
            return;
        }

        int cleared = _pendingClear.Count;
        var clearSet = new HashSet<int>(_pendingClear);

        // 消去対象でない行を下から順に詰め直す。
        int writeRow = Rows - 1;
        for (int readRow = Rows - 1; readRow >= 0; readRow--)
        {
            if (clearSet.Contains(readRow))
            {
                continue;
            }
            if (writeRow != readRow)
            {
                for (int x = 0; x < Columns; x++)
                {
                    Grid[writeRow, x] = Grid[readRow, x];
                }
            }
            writeRow--;
        }
        // 詰めた残りの上部を空にする。
        for (int row = writeRow; row >= 0; row--)
        {
            for (int x = 0; x < Columns; x++)
            {
                Grid[row, x] = null;
            }
        }

        Lines += cleared;
        // 消したライン数に応じた得点（レベル補正あり）。
        int[] table = { 0, 100, 300, 500, 800 };
        Score += table[cleared] * Level;

        _pendingClear.Clear();
        SpawnNext();
    }

    /// <summary>テスト用: 現在の落下ピースを差し替える（決定的な盤面を作るため）。</summary>
    internal void SetCurrentForTest(Tetromino piece) => Current = piece;

    /// <summary>テスト用: 現在のピースをその場で固定し、満杯行の検出まで行う。</summary>
    internal void LockCurrentForTest() => LockPiece();

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
