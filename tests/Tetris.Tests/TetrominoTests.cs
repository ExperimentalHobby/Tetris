using System.Linq;
using Tetris;

namespace Tetris.Tests;

/// <summary>
/// <see cref="Tetromino"/> の形状・回転・座標計算を検証するテスト。
/// </summary>
public class TetrominoTests
{
	/// <summary>
	/// 全テトロミノ種に色が定義されていることを確認する。
	/// パス条件: すべての <see cref="TetrominoType"/> が <see cref="Tetromino.Colors"/> のキーに存在する。
	/// </summary>
	[Fact]
	public void ColorsDefinesEveryTetrominoType()
	{
		foreach (TetrominoType type in System.Enum.GetValues<TetrominoType>())
		{
			Assert.True(Tetromino.Colors.ContainsKey(type), $"{type} の色が未定義");
		}
	}

	/// <summary>
	/// O ピースは回転しても形が変わらないことを確認する。
	/// パス条件: 回転後の <c>Cells</c> が回転前と一致する。
	/// </summary>
	[Fact]
	public void RotatedOPieceKeepsSameShape()
	{
		var o = new Tetromino(TetrominoType.O);
		var rotated = o.Rotated();

		Assert.True(CellsEqual(o.Cells, rotated.Cells));
	}

	/// <summary>
	/// 4 回回転すると元の姿勢に戻ることを確認する。
	/// パス条件: 4 回回転後の <c>Cells</c> が初期形状と一致する。
	/// </summary>
	[Fact]
	public void RotatedFourTimesReturnsToOriginalShape()
	{
		var piece = new Tetromino(TetrominoType.T);
		var original = (bool[,])piece.Cells.Clone();

		var result = piece.Rotated().Rotated().Rotated().Rotated();

		Assert.True(CellsEqual(original, result.Cells));
	}

	/// <summary>
	/// 時計回り回転で RotationState が 1 ずつ進み、4 回で 0 に戻ることを確認する。
	/// パス条件: 0→1→2→3→0 の順に変化する。
	/// </summary>
	[Fact]
	public void RotatedUpdatesRotationState()
	{
		var piece = new Tetromino(TetrominoType.T);
		Assert.Equal(0, piece.RotationState);

		var r1 = piece.Rotated();
		Assert.Equal(1, r1.RotationState);

		var r2 = r1.Rotated();
		Assert.Equal(2, r2.RotationState);

		var r3 = r2.Rotated();
		Assert.Equal(3, r3.RotationState);

		var r4 = r3.Rotated();
		Assert.Equal(0, r4.RotationState);
	}

	/// <summary>
	/// 反時計回り回転で RotationState が 1 ずつ戻ることを確認する。
	/// パス条件: 0→3→2→1→0 の順に変化する。
	/// </summary>
	[Fact]
	public void RotatedCcwUpdatesRotationState()
	{
		var piece = new Tetromino(TetrominoType.T);

		var r1 = piece.RotatedCcw();
		Assert.Equal(3, r1.RotationState);

		var r2 = r1.RotatedCcw();
		Assert.Equal(2, r2.RotationState);
	}

	/// <summary>
	/// T ピースが時計回り 90 度に正しく回転することを確認する。
	/// パス条件: 回転後の <c>Cells</c> が期待する時計回り姿勢と一致する。
	/// </summary>
	[Fact]
	public void RotatedTPieceMatchesClockwiseRotation()
	{
		// T の初期姿勢（3x3）:
		//   . X .
		//   X X X
		//   . . .
		var t = new Tetromino(TetrominoType.T);
		var rotated = t.Rotated();

		// 時計回り 90 度:
		//   . X .
		//   . X X
		//   . X .
		var expected = new[,]
		{
			{ false, true,  false },
			{ false, true,  true  },
			{ false, true,  false },
		};
		Assert.True(CellsEqual(expected, rotated.Cells));
	}

	/// <summary>
	/// T ピースが反時計回り 90 度に正しく回転することを確認する。
	/// パス条件: 回転後の <c>Cells</c> が期待する反時計回り姿勢と一致する。
	/// </summary>
	[Fact]
	public void RotatedCcwTPieceMatchesCounterClockwiseRotation()
	{
		// T の初期姿勢（3x3）:
		//   . X .
		//   X X X
		//   . . .
		var t = new Tetromino(TetrominoType.T);
		var rotated = t.RotatedCcw();

		// 反時計回り 90 度:
		//   . X .
		//   X X .
		//   . X .
		var expected = new[,]
		{
			{ false, true,  false },
			{ true,  true,  false },
			{ false, true,  false },
		};
		Assert.True(CellsEqual(expected, rotated.Cells));
	}

	/// <summary>
	/// 4 回反時計回転すると元の姿勢に戻ることを確認する。
	/// パス条件: 4 回反時計回転後の <c>Cells</c> が初期形状と一致する。
	/// </summary>
	[Fact]
	public void RotatedCcwFourTimesReturnsToOriginalShape()
	{
		var piece = new Tetromino(TetrominoType.T);
		var original = (bool[,])piece.Cells.Clone();

		var result = piece.RotatedCcw().RotatedCcw().RotatedCcw().RotatedCcw();

		Assert.True(CellsEqual(original, result.Cells));
	}

	/// <summary>
	/// O ピースは反時計回転しても形が変わらないことを確認する。
	/// パス条件: 反時計回転後の <c>Cells</c> が回転前と一致する。
	/// </summary>
	[Fact]
	public void RotatedCcwOPieceKeepsSameShape()
	{
		var o = new Tetromino(TetrominoType.O);
		var rotated = o.RotatedCcw();

		Assert.True(CellsEqual(o.Cells, rotated.Cells));
	}

	/// <summary>
	/// <see cref="Tetromino.Blocks"/> が位置 (X, Y) を加味した盤面座標を返すことを確認する。
	/// パス条件: T ピースを (4,5) に置いたときの占有セルが期待値と一致する。
	/// </summary>
	[Fact]
	public void BlocksAreOffsetByPosition()
	{
		var t = new Tetromino(TetrominoType.T) { X = 4, Y = 5 };

		var blocks = t.Blocks().OrderBy(b => b.Y).ThenBy(b => b.X).ToList();

		var expected = new[]
		{
			(X: 5, Y: 5), // (row0,col1)
			(X: 4, Y: 6), // (row1,col0)
			(X: 5, Y: 6), // (row1,col1)
			(X: 6, Y: 6), // (row1,col2)
		};
		Assert.Equal(expected, blocks);
	}

	/// <summary>
	/// <see cref="Tetromino.Clone"/> が元へ影響しない独立コピーを返すことを確認する。
	/// パス条件: クローンの位置を変更しても元の位置は変わらず、形状は一致する。
	/// </summary>
	[Fact]
	public void CloneIsIndependentCopy()
	{
		var piece = new Tetromino(TetrominoType.L) { X = 2, Y = 3 };
		var clone = piece.Clone();
		clone.X = 9;

		Assert.Equal(2, piece.X);
		Assert.Equal(9, clone.X);
		Assert.True(CellsEqual(piece.Cells, clone.Cells));
	}

	/// <summary>2 つの正方行列（セル配列）が同一形状・同一内容かを判定する。</summary>
	private static bool CellsEqual(bool[,] a, bool[,] b)
	{
		if (a.GetLength(0) != b.GetLength(0) || a.GetLength(1) != b.GetLength(1))
		{
			return false;
		}
		for (int y = 0; y < a.GetLength(0); y++)
		{
			for (int x = 0; x < a.GetLength(1); x++)
			{
				if (a[y, x] != b[y, x])
				{
					return false;
				}
			}
		}
		return true;
	}
}
