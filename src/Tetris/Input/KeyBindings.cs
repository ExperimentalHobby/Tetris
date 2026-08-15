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
    /// </summary>
    public static KeyBindings FromSaved(IReadOnlyDictionary<GameAction, Key> saved)
    {
        var bindings = Default();
        foreach (var action in Enum.GetValues<GameAction>())
        {
            if (saved.TryGetValue(action, out var key))
            {
                bindings._map[action] = key;
            }
        }
        return bindings;
    }

    /// <summary>指定した操作に割り当てられているキーを返す。</summary>
    public Key GetKey(GameAction action) => _map[action];

    /// <summary>永続化用に全操作とキーの対応を読み取り専用の辞書として返す。</summary>
    public IReadOnlyDictionary<GameAction, Key> ToDictionary() => _map;

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
