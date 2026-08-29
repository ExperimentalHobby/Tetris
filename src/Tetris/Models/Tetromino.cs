using System.Windows.Media;

namespace Tetris;

/// <summary>
/// テトロミノの種類。
/// </summary>
public enum TetrominoType
{
	I,
	O,
	T,
	S,
	Z,
	J,
	L
}

/// <summary>
/// 1 つのテトロミノ（落下ピース）。形状を正方行列で保持し、行列回転で姿勢を変える。
/// </summary>
public sealed class Tetromino
{
	private static readonly Dictionary<TetrominoType, bool[,]> Shapes = new()
	{
		[TetrominoType.I] = new[,]
		{
			{ false, false, false, false },
			{ true,  true,  true,  true  },
			{ false, false, false, false },
			{ false, false, false, false },
		},
		[TetrominoType.O] = new[,]
		{
			{ true, true },
			{ true, true },
		},
		[TetrominoType.T] = new[,]
		{
			{ false, true,  false },
			{ true,  true,  true  },
			{ false, false, false },
		},
		[TetrominoType.S] = new[,]
		{
			{ false, true,  true  },
			{ true,  true,  false },
			{ false, false, false },
		},
		[TetrominoType.Z] = new[,]
		{
			{ true,  true,  false },
			{ false, true,  true  },
			{ false, false, false },
		},
		[TetrominoType.J] = new[,]
		{
			{ true,  false, false },
			{ true,  true,  true  },
			{ false, false, false },
		},
		[TetrominoType.L] = new[,]
		{
			{ false, false, true  },
			{ true,  true,  true  },
			{ false, false, false },
		},
	};

	public static readonly Dictionary<TetrominoType, Color> Colors = new()
	{
		[TetrominoType.I] = Color.FromRgb(0x00, 0xF0, 0xF0), // シアン
		[TetrominoType.O] = Color.FromRgb(0xF0, 0xF0, 0x00), // 黄
		[TetrominoType.T] = Color.FromRgb(0xA0, 0x00, 0xF0), // 紫
		[TetrominoType.S] = Color.FromRgb(0x00, 0xF0, 0x00), // 緑
		[TetrominoType.Z] = Color.FromRgb(0xF0, 0x00, 0x00), // 赤
		[TetrominoType.J] = Color.FromRgb(0x00, 0x00, 0xF0), // 青
		[TetrominoType.L] = Color.FromRgb(0xF0, 0xA0, 0x00), // オレンジ
	};

	public TetrominoType Type { get; }

	/// <summary>現在の姿勢を表す正方行列（true がブロック）。</summary>
	public bool[,] Cells { get; private set; }

	/// <summary>盤面上での左上セルの位置（X=列, Y=行）。</summary>
	public int X { get; set; }
	public int Y { get; set; }

	public int Size => Cells.GetLength(0);

	/// <summary>回転姿勢(SRS準拠): 0=spawn, 1=R(時計回り1回), 2=180度, 3=L(反時計回り1回)。</summary>
	public int RotationState { get; private set; }

	public Tetromino(TetrominoType type)
	{
		Type = type;
		var src = Shapes[type];
		Cells = (bool[,])src.Clone();
		RotationState = 0;
	}

	private Tetromino(TetrominoType type, bool[,] cells, int x, int y, int rotationState)
	{
		Type = type;
		Cells = cells;
		X = x;
		Y = y;
		RotationState = rotationState;
	}

	/// <summary>現在の状態を複製する。</summary>
	public Tetromino Clone() => new(Type, (bool[,])Cells.Clone(), X, Y, RotationState);

	/// <summary>時計回りに 90 度回転させた新しいピースを返す。</summary>
	public Tetromino Rotated()
	{
		int n = Size;
		var rotated = new bool[n, n];
		for (int y = 0; y < n; y++)
		{
			for (int x = 0; x < n; x++)
			{
				rotated[x, n - 1 - y] = Cells[y, x];
			}
		}
		return new Tetromino(Type, rotated, X, Y, (RotationState + 1) % 4);
	}

	/// <summary>反時計回りに 90 度回転させた新しいピースを返す。</summary>
	public Tetromino RotatedCcw()
	{
		int n = Size;
		var rotated = new bool[n, n];
		for (int y = 0; y < n; y++)
		{
			for (int x = 0; x < n; x++)
			{
				rotated[n - 1 - x, y] = Cells[y, x];
			}
		}
		return new Tetromino(Type, rotated, X, Y, (RotationState + 3) % 4);
	}

	/// <summary>このピースが占有する盤面セルを列挙する（X=列, Y=行）。</summary>
	public IEnumerable<(int X, int Y)> Blocks()
	{
		int n = Size;
		for (int y = 0; y < n; y++)
		{
			for (int x = 0; x < n; x++)
			{
				if (Cells[y, x])
				{
					yield return (X + x, Y + y);
				}
			}
		}
	}
}
