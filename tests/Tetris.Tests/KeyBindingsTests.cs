using System.Windows.Input;
using Tetris.Input;

namespace Tetris.Tests;

/// <summary>
/// <see cref="KeyBindings"/> の既定値・変更・重複入れ替えを検証するテスト。
/// </summary>
public class KeyBindingsTests
{
	/// <summary>
	/// 既定値が現行のハードコードされていたキー割り当てと一致することを確認する。
	/// パス条件: Default の各操作が旧 MainWindow.xaml の InputBindings と同じキーを返す。
	/// </summary>
	[Fact]
	public void DefaultMatchesLegacyHardcodedKeys()
	{
		var bindings = KeyBindings.Default();

		Assert.Equal(Key.Left, bindings.GetKey(GameAction.MoveLeft));
		Assert.Equal(Key.Right, bindings.GetKey(GameAction.MoveRight));
		Assert.Equal(Key.Up, bindings.GetKey(GameAction.Rotate));
		Assert.Equal(Key.Z, bindings.GetKey(GameAction.RotateCcw));
		Assert.Equal(Key.Down, bindings.GetKey(GameAction.SoftDrop));
		Assert.Equal(Key.Space, bindings.GetKey(GameAction.HardDrop));
		Assert.Equal(Key.C, bindings.GetKey(GameAction.Hold));
		Assert.Equal(Key.Return, bindings.GetKey(GameAction.Start));
		Assert.Equal(Key.P, bindings.GetKey(GameAction.Pause));
		Assert.Equal(Key.M, bindings.GetKey(GameAction.ToggleMute));
	}

	/// <summary>
	/// 他操作と重複しない新しいキーへの変更が反映されることを確認する。
	/// パス条件: TrySetKey(Rotate, Key.X) の後、GetKey(Rotate) が Key.X を返す。
	/// </summary>
	[Fact]
	public void TrySetKeyWithUnusedKeyUpdatesBinding()
	{
		var bindings = KeyBindings.Default();

		bindings.TrySetKey(GameAction.Rotate, Key.X);

		Assert.Equal(Key.X, bindings.GetKey(GameAction.Rotate));
	}

	/// <summary>
	/// 既に他操作に割り当て済みのキーを指定すると、双方のキーが入れ替わることを確認する。
	/// パス条件: Hold(既定 Key.C) に Rotate の既定キー(Key.Up) を割り当てると、
	/// Rotate は Hold の元のキー(Key.C) に、Hold は Key.Up になる。
	/// </summary>
	[Fact]
	public void TrySetKeyWithKeyAlreadyUsedByAnotherActionSwapsBindings()
	{
		var bindings = KeyBindings.Default();

		bindings.TrySetKey(GameAction.Hold, Key.Up);

		Assert.Equal(Key.Up, bindings.GetKey(GameAction.Hold));
		Assert.Equal(Key.C, bindings.GetKey(GameAction.Rotate));
	}

	/// <summary>
	/// キーから対応する操作を逆引きできることを確認する（MainWindow のディスパッチで使用）。
	/// パス条件: ActionFor(Key.Up) が GameAction.Rotate を返す。
	/// </summary>
	[Fact]
	public void ActionForWithBoundKeyReturnsMatchingAction()
	{
		var bindings = KeyBindings.Default();

		var action = bindings.ActionFor(Key.Up);

		Assert.Equal(GameAction.Rotate, action);
	}

	/// <summary>
	/// どの操作にも割り当てられていないキーを渡すと null を返すことを確認する。
	/// パス条件: ActionFor(Key.F1) が null。
	/// </summary>
	[Fact]
	public void ActionForWithUnboundKeyReturnsNull()
	{
		var bindings = KeyBindings.Default();

		var action = bindings.ActionFor(Key.F1);

		Assert.Null(action);
	}
}
