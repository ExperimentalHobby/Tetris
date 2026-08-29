using Tetris;

namespace Tetris.Tests;

/// <summary>
/// <see cref="GameEngine"/> のホールド（ピース保管）機能を検証するテスト。
/// </summary>
public class GameEngineHoldTests
{
	private static GameEngine StartedEngine()
	{
		var engine = new GameEngine();
		engine.Start();
		return engine;
	}

	/// <summary>
	/// 開始直後はホールドが空で、ホールド可能であることを確認する。
	/// パス条件: HeldType が null、CanHold が true。
	/// </summary>
	[Fact]
	public void StartHasEmptyHoldAndCanHold()
	{
		var engine = StartedEngine();

		Assert.Null(engine.HeldType);
		Assert.True(engine.CanHold);
	}

	/// <summary>
	/// 初回ホールドで現在のピースが保管され、NEXT が降りてくることを確認する。
	/// パス条件: HeldType が元の現在ピース種、現在ピースが元の NEXT 種、CanHold が false。
	/// </summary>
	[Fact]
	public void HoldFirstTimeStoresCurrentAndSpawnsNext()
	{
		var engine = StartedEngine();
		engine.SetCurrentForTest(new Tetromino(TetrominoType.T) { X = 3, Y = 0 });
		var expectedNext = engine.NextType;

		engine.Hold();

		Assert.Equal(TetrominoType.T, engine.HeldType);
		Assert.Equal(expectedNext, engine.Current!.Type);
		Assert.False(engine.CanHold);
	}

	/// <summary>
	/// 設置前の連続ホールドは無視されることを確認する（1 ピース 1 回まで）。
	/// パス条件: 2 回目の Hold で HeldType・現在ピースが変化しない。
	/// </summary>
	[Fact]
	public void HoldTwiceWithoutLockIsIgnored()
	{
		var engine = StartedEngine();
		engine.SetCurrentForTest(new Tetromino(TetrominoType.T) { X = 3, Y = 0 });

		engine.Hold();
		var heldAfterFirst = engine.HeldType;
		var currentAfterFirst = engine.Current!.Type;

		engine.Hold();

		Assert.Equal(heldAfterFirst, engine.HeldType);
		Assert.Equal(currentAfterFirst, engine.Current!.Type);
	}

	/// <summary>
	/// 設置後はホールドが再び可能になり、保管ピースと入れ替わることを確認する。
	/// パス条件: 固定で CanHold が回復し、再ホールドで現在ピースと保管ピースが入れ替わる。
	/// </summary>
	[Fact]
	public void HoldAfterLockSwapsWithHeldPiece()
	{
		var engine = StartedEngine();
		engine.SetCurrentForTest(new Tetromino(TetrominoType.T) { X = 3, Y = 0 });

		engine.Hold();                 // HeldType = T、現在は別ピース
		Assert.False(engine.CanHold);

		engine.HardDrop();             // 底まで落として設置 → 再びホールド可能に
		Assert.True(engine.CanHold);

		var currentBeforeSwap = engine.Current!.Type;
		engine.Hold();                 // 保管(T)と入れ替え

		Assert.Equal(TetrominoType.T, engine.Current!.Type);
		Assert.Equal(currentBeforeSwap, engine.HeldType);
		Assert.False(engine.CanHold);
	}

	/// <summary>
	/// 開始でホールド状態がリセットされることを確認する。
	/// パス条件: ホールド後に Start すると HeldType が null、CanHold が true に戻る。
	/// </summary>
	[Fact]
	public void StartResetsHoldState()
	{
		var engine = StartedEngine();
		engine.Hold();

		engine.Start();

		Assert.Null(engine.HeldType);
		Assert.True(engine.CanHold);
	}

	/// <summary>
	/// 開始前はホールドしても何も起きないことを確認する。
	/// パス条件: 例外を投げず、HeldType が null のまま。
	/// </summary>
	[Fact]
	public void HoldBeforeStartDoesNothing()
	{
		var engine = new GameEngine();

		engine.Hold();

		Assert.Null(engine.HeldType);
	}
}
