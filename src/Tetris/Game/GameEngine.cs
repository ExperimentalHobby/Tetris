namespace Tetris;

/// <summary>
/// テトリスの盤面とゲーム進行を管理するエンジン。描画・入力からは独立している。
/// </summary>
public sealed class GameEngine
{
	public const int Columns = 10;
	public const int Rows = 20;

	/// <summary>NEXT 欄で先読み表示する個数。</summary>
	public const int PreviewCount = 3;

	/// <summary>SRS準拠のウォールキックテーブル(JLSTZ用)。キーは(回転前状態,回転後状態)。値は5候補のオフセット(dx,dy)。
	/// 標準のSRSデータはY上方向を正として定義されるため、本エンジンのY下方向正の座標系に合わせて符号を反転済み。</summary>
	private static readonly Dictionary<(int From, int To), (int Dx, int Dy)[]> JlstzKicks = new()
	{
		[(0, 1)] = new[] { (0, 0), (-1, 0), (-1, -1), (0, 2), (-1, 2) },
		[(1, 0)] = new[] { (0, 0), (1, 0), (1, 1), (0, -2), (1, -2) },
		[(1, 2)] = new[] { (0, 0), (1, 0), (1, -1), (0, 2), (1, 2) },
		[(2, 1)] = new[] { (0, 0), (-1, 0), (-1, 1), (0, -2), (-1, -2) },
		[(2, 3)] = new[] { (0, 0), (1, 0), (1, 1), (0, -2), (1, -2) },
		[(3, 2)] = new[] { (0, 0), (-1, 0), (-1, -1), (0, 2), (-1, 2) },
		[(3, 0)] = new[] { (0, 0), (-1, 0), (-1, 1), (0, -2), (-1, -2) },
		[(0, 3)] = new[] { (0, 0), (1, 0), (1, -1), (0, 2), (1, 2) },
	};

	/// <summary>SRS準拠のウォールキックテーブル(I用)。JLSTZ用より大きいオフセットを使う。</summary>
	private static readonly Dictionary<(int From, int To), (int Dx, int Dy)[]> IKicks = new()
	{
		[(0, 1)] = new[] { (0, 0), (-2, 0), (1, 0), (-2, 1), (1, -2) },
		[(1, 0)] = new[] { (0, 0), (2, 0), (-1, 0), (2, -1), (-1, 2) },
		[(1, 2)] = new[] { (0, 0), (-1, 0), (2, 0), (-1, -2), (2, 1) },
		[(2, 1)] = new[] { (0, 0), (1, 0), (-2, 0), (1, 2), (-2, -1) },
		[(2, 3)] = new[] { (0, 0), (2, 0), (-1, 0), (2, -1), (-1, 2) },
		[(3, 2)] = new[] { (0, 0), (-2, 0), (1, 0), (-2, 1), (1, -2) },
		[(3, 0)] = new[] { (0, 0), (1, 0), (-2, 0), (1, 2), (-2, -1) },
		[(0, 3)] = new[] { (0, 0), (-1, 0), (2, 0), (-1, -2), (2, 1) },
	};

	/// <summary>回転にキックが不要なピース(O)用の単一オフセット。</summary>
	private static readonly (int Dx, int Dy)[] NoKick = new[] { (0, 0) };

	/// <summary>T-Spinの種別。3コーナールールにより判定する（Mini/Fullの簡易判定）。</summary>
	private enum TSpinKind
	{
		None,
		Mini,
		Full,
	}

	private readonly Random _random = new();
	private readonly Queue<TetrominoType> _bag = new();
	private readonly List<TetrominoType> _nextQueue = new();
	private readonly List<int> _pendingClear = new();

	/// <summary>直近の成功アクションが回転だったかどうか（T-Spin判定用）。移動成功でfalse、回転成功でtrueになる。</summary>
	private bool _lastActionWasRotation;

	/// <summary>直近のロックで検出されたT-Spinの種別。LockPieceで設定し、CommitClear/LockPieceの加点時に消費する。</summary>
	private TSpinKind _pendingTSpinKind;

	/// <summary>接地してから固定するまでの猶予時間。</summary>
	private static readonly TimeSpan LockDelayDuration = TimeSpan.FromMilliseconds(500);

	/// <summary>ロックディレイをリセットできる最大回数（無限の設置回避のため）。</summary>
	public const int MaxLockResets = 15;

	private TimeSpan _lockDelayElapsed = TimeSpan.Zero;
	private int _lockResetCount;

	/// <summary>固定済みブロックの色。null は空セル。</summary>
	public TetrominoType?[,] Grid { get; } = new TetrominoType?[Rows, Columns];

	public Tetromino? Current { get; private set; }

	/// <summary>出現したピースの総数（統計表示用）。</summary>
	public int PieceCount { get; private set; }

	/// <summary>テトリス（4ライン同時消し）を決めた回数（統計表示用）。</summary>
	public int TetrisCount { get; private set; }

	/// <summary>ライン消去を伴った固定の総回数（統計表示用）。</summary>
	public int TotalClearCount { get; private set; }

	/// <summary>ライン消去のうちテトリスだった割合(%)。消去実績が無ければ 0。</summary>
	public double TetrisRate => TotalClearCount == 0 ? 0 : (double)TetrisCount / TotalClearCount * 100;

	/// <summary>先読み表示用の次ピース列（先頭がすぐ次に出現する種）。</summary>
	public IReadOnlyList<TetrominoType> NextQueue => _nextQueue;

	/// <summary>次に出現するピース種（<see cref="NextQueue"/> の先頭と同じ）。</summary>
	public TetrominoType NextType => _nextQueue[0];

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

	/// <summary>連続してライン消去に成功した回数。消去を伴わない固定で 0 にリセットされる。</summary>
	public int Combo { get; private set; }

	/// <summary>直近の消去が Back-to-Back（連続テトリス等の難しい消去）だったかどうか。</summary>
	public bool IsBackToBack { get; private set; }

	private bool _lastClearWasDifficult;

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
		_nextQueue.Clear();
		_pendingClear.Clear();
		Score = 0;
		Lines = 0;
		IsGameOver = false;
		HeldType = null;
		CanHold = true;
		_lastActionWasRotation = false;
		_pendingTSpinKind = TSpinKind.None;
		Combo = 0;
		IsBackToBack = false;
		_lastClearWasDifficult = false;
		PieceCount = 0;
		TetrisCount = 0;
		TotalClearCount = 0;
		for (int i = 0; i < PreviewCount; i++)
		{
			_nextQueue.Add(NextFromBag());
		}
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
		var type = _nextQueue[0];
		_nextQueue.RemoveAt(0);
		_nextQueue.Add(NextFromBag());
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
		PieceCount++;
		_lastActionWasRotation = false;
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

	/// <summary>プレイヤー操作による 1 段落下を試みる。接地している場合はロックディレイ猶予中として何もしない。</summary>
	public void SoftDrop()
	{
		if (StepDown())
		{
			Score += 1; // ソフトドロップのボーナス
		}
	}

	/// <summary>重力による自然落下で 1 段下げる。プレイヤー操作ではないため加点しない。</summary>
	public void GravityDrop() => StepDown();

	/// <summary>1 段落下を試みる共通処理。接地している場合はロックディレイ猶予中として何もしない。</summary>
	/// <returns>移動できた場合 true。</returns>
	private bool StepDown()
	{
		if (IsGameOver || Current is null)
		{
			return false;
		}
		return TryMove(0, 1);
	}

	/// <summary>接地からの経過時間を進める。ロックディレイを超えたら固定する。非接地なら経過時間をリセットする。</summary>
	/// <returns>この呼び出しでピースを固定した場合 true。</returns>
	public bool AdvanceLockDelay(TimeSpan elapsed)
	{
		if (IsGameOver || Current is null || !IsGrounded)
		{
			_lockDelayElapsed = TimeSpan.Zero;
			return false;
		}
		_lockDelayElapsed += elapsed;
		if (_lockDelayElapsed >= LockDelayDuration)
		{
			LockPiece();
			return true;
		}
		return false;
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

	public bool Rotate() => TryRotate(piece => piece.Rotated(), 1);

	/// <summary>反時計回りに回転を試みる（SRS準拠のウォールキックあり）。</summary>
	public bool RotateCcw() => TryRotate(piece => piece.RotatedCcw(), 3);

	/// <summary>
	/// 回転を試みる。SRS準拠のウォールキックテーブルに従い、複数のオフセットを順に試す。
	/// </summary>
	/// <param name="rotate">回転後の姿勢を生成する関数。</param>
	/// <param name="toStateOffset">回転前状態からの相対オフセット(時計回り=1, 反時計回り=3)。</param>
	private bool TryRotate(Func<Tetromino, Tetromino> rotate, int toStateOffset)
	{
		if (IsGameOver || Current is null)
		{
			return false;
		}
		int fromState = Current.RotationState;
		int toState = (fromState + toStateOffset) % 4;
		var rotated = rotate(Current);
		var offsets = GetKickOffsets(Current.Type, fromState, toState);
		foreach (var (dx, dy) in offsets)
		{
			var test = rotated.Clone();
			test.X += dx;
			test.Y += dy;
			if (IsValid(test))
			{
				Current = test;
				OnSuccessfulAction();
				_lastActionWasRotation = true;
				return true;
			}
		}
		return false;
	}

	/// <summary>ピース種と回転前後の状態に応じたウォールキックのオフセット候補を返す。</summary>
	private static (int Dx, int Dy)[] GetKickOffsets(TetrominoType type, int fromState, int toState)
	{
		if (type == TetrominoType.O)
		{
			return NoKick;
		}
		var table = type == TetrominoType.I ? IKicks : JlstzKicks;
		return table[(fromState, toState)];
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
			_lastActionWasRotation = false;
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
		_pendingTSpinKind = DetectTSpin(Current, _lastActionWasRotation);
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
			// ライン消去を伴わない T-Spin は固定点ボーナスをここで即加算する。
			if (_pendingTSpinKind == TSpinKind.Full)
			{
				Score += 400 * Level;
			}
			else if (_pendingTSpinKind == TSpinKind.Mini)
			{
				Score += 100 * Level;
			}
			_pendingTSpinKind = TSpinKind.None;
			Combo = 0; // 消去を伴わない固定でコンボが途切れる。
			SpawnNext();
		}
	}

	/// <summary>
	/// 3コーナールールに基づき T-Spin の種別を判定する（簡易版: SRSの「5番目のキック特例」は扱わない）。
	/// 直前の成功アクションが回転でない、またはピース種が T でない場合は None。
	/// </summary>
	private TSpinKind DetectTSpin(Tetromino piece, bool lastActionWasRotation)
	{
		if (!lastActionWasRotation || piece.Type != TetrominoType.T)
		{
			return TSpinKind.None;
		}

		int centerX = piece.X + 1;
		int centerY = piece.Y + 1;

		bool IsOccupied(int x, int y) => x < 0 || x >= Columns || y < 0 || y >= Rows || Grid[y, x] is not null;

		bool tl = IsOccupied(centerX - 1, centerY - 1);
		bool tr = IsOccupied(centerX + 1, centerY - 1);
		bool bl = IsOccupied(centerX - 1, centerY + 1);
		bool br = IsOccupied(centerX + 1, centerY + 1);

		// 尖端(point)方向の2隅を「前方」、その反対側を「後方」とする。
		var (front1, front2, back1, back2) = piece.RotationState switch
		{
			0 => (tl, tr, bl, br), // 尖端: 上
			1 => (tr, br, tl, bl), // 尖端: 右
			2 => (bl, br, tl, tr), // 尖端: 下
			_ => (tl, bl, tr, br), // 尖端: 左
		};

		int frontCount = (front1 ? 1 : 0) + (front2 ? 1 : 0);
		int backCount = (back1 ? 1 : 0) + (back2 ? 1 : 0);

		if (frontCount + backCount < 3)
		{
			return TSpinKind.None;
		}
		return frontCount == 2 ? TSpinKind.Full : TSpinKind.Mini;
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
		TotalClearCount++;
		if (cleared == 4)
		{
			TetrisCount++;
		}

		int baseScore;
		if (_pendingTSpinKind == TSpinKind.Full)
		{
			// T-Spin Full: 1/2/3 ライン消去（×Level）。
			int[] tSpinFullTable = { 0, 800, 1200, 1600 };
			baseScore = tSpinFullTable[cleared] * Level;
		}
		else if (_pendingTSpinKind == TSpinKind.Mini)
		{
			// T-Spin Mini はピースの性質上、通常 1 ラインまでしか消去できない。
			baseScore = (cleared == 1 ? 200 : 800 * cleared) * Level;
		}
		else
		{
			// 消したライン数に応じた通常の得点（レベル補正あり）。
			int[] table = { 0, 100, 300, 500, 800 };
			baseScore = table[cleared] * Level;
		}

		// Perfect Clear（全消し）: 消去後に盤面が完全に空になった場合、通常/T-Spinの得点を専用テーブルで上書きする。
		if (IsBoardEmpty())
		{
			int[] perfectClearTable = { 0, 800, 1200, 1800, 2000 };
			baseScore = perfectClearTable[cleared] * Level;
		}

		// コンボ: 消去が連続するほど加点（1回目はボーナスなし）。
		Combo++;
		int comboBonus = 50 * (Combo - 1) * Level;

		// Back-to-Back: テトリス（4ライン同時消し）またはT-Spinによるライン消去が連続すると基礎点の+50%を加算する。
		bool isDifficult = cleared == 4 || _pendingTSpinKind != TSpinKind.None;
		bool isBackToBack = isDifficult && _lastClearWasDifficult;
		int backToBackBonus = isBackToBack ? baseScore / 2 : 0;
		IsBackToBack = isBackToBack;
		_lastClearWasDifficult = isDifficult;

		Score += baseScore + comboBonus + backToBackBonus;
		_pendingTSpinKind = TSpinKind.None;

		_pendingClear.Clear();
		SpawnNext();
	}

	/// <summary>盤面に固定ブロックが1つも無い（Perfect Clear）状態かどうかを返す。</summary>
	private bool IsBoardEmpty()
	{
		for (int y = 0; y < Rows; y++)
		{
			for (int x = 0; x < Columns; x++)
			{
				if (Grid[y, x] is not null)
				{
					return false;
				}
			}
		}
		return true;
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
