using System.Windows.Input;

namespace Tetris.Input;

/// <summary>
/// <see cref="GameAction"/> とキーの対応関係を保持する。キーコンフィグのリマップ・永続化の対象。
/// </summary>
public sealed class KeyBindings
{
	private readonly Dictionary<GameAction, Key> _map;

	private KeyBindings(Dictionary<GameAction, Key> map)
	{
		_map = map;
	}

	/// <summary>既定のキー割り当てを返す（旧 MainWindow.xaml の InputBindings と同一）。</summary>
	public static KeyBindings Default() => new(new Dictionary<GameAction, Key>
	{
		[GameAction.MoveLeft] = Key.Left,
		[GameAction.MoveRight] = Key.Right,
		[GameAction.Rotate] = Key.Up,
		[GameAction.RotateCcw] = Key.Z,
		[GameAction.SoftDrop] = Key.Down,
		[GameAction.HardDrop] = Key.Space,
		[GameAction.Hold] = Key.C,
		[GameAction.Start] = Key.Return,
		[GameAction.Pause] = Key.P,
		[GameAction.ToggleMute] = Key.M,
	});

	/// <summary>
	/// 保存済みの辞書から復元する。未知/欠落した操作は既定値で補う（バージョン間の互換性のため）。
	/// 保存内容に重複したキー割り当てが含まれる場合（保存ファイルの手動改変等）、列挙順で先に処理された
	/// 操作を優先し、後から処理された操作は既定値にフォールバックする（衝突する2操作が同じキーを
	/// 取り合う事態を避けるため）。既定値どうしの衝突など、保存値以外が原因の衝突までは解決しない。
	/// </summary>
	public static KeyBindings FromSaved(IReadOnlyDictionary<GameAction, Key> saved)
	{
		var bindings = Default();
		var usedKeys = new HashSet<Key>();
		foreach (var action in Enum.GetValues<GameAction>())
		{
			if (saved.TryGetValue(action, out var key) && usedKeys.Add(key))
			{
				bindings._map[action] = key;
			}
			else
			{
				// 保存値が無い、または既に他の操作が使用しているキーと重複する場合は既定値のまま。
				// 以降の操作の重複判定にも使うため、既定値も使用済みキーとして登録する。
				usedKeys.Add(bindings._map[action]);
			}
		}
		return bindings;
	}

	/// <summary>指定した操作に割り当てられているキーを返す。</summary>
	public Key GetKey(GameAction action) => _map[action];

	/// <summary>
	/// 永続化用に全操作とキーの対応を辞書として返す。
	/// 呼び出し側の変更が内部状態に波及しないようコピーを返す。
	/// </summary>
	public IReadOnlyDictionary<GameAction, Key> ToDictionary() => new Dictionary<GameAction, Key>(_map);

	/// <summary>指定したキーに割り当てられている操作を返す。どの操作にも割り当てられていなければ null。</summary>
	public GameAction? ActionFor(Key key)
	{
		foreach (var (action, boundKey) in _map)
		{
			if (boundKey == key)
			{
				return action;
			}
		}
		return null;
	}

	/// <summary>
	/// 指定した操作に新しいキーを割り当てる。
	/// 既に他の操作がそのキーを使っている場合は、その操作には元のキーを譲る形で入れ替える。
	/// </summary>
	public void TrySetKey(GameAction action, Key key)
	{
		// 列挙中に辞書を書き換えないよう、まず衝突する操作を確定してから代入する。
		GameAction? conflictingAction = _map
			.Where(kv => kv.Key != action && kv.Value == key)
			.Select(kv => (GameAction?)kv.Key)
			.FirstOrDefault();

		if (conflictingAction is { } conflict)
		{
			_map[conflict] = _map[action];
		}
		_map[action] = key;
	}
}
