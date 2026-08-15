using System.Windows.Input;

namespace Tetris.Input;

/// <summary>
/// <see cref="Key"/> を画面表示用の短い文字列に変換する（矢印キー等は記号化する）。
/// </summary>
public static class KeyDisplay
{
    /// <summary>表示用の文字列を返す。矢印キー・Enter・Space 以外は列挙値名をそのまま使う。</summary>
    public static string ToDisplayString(Key key) => key switch
    {
        Key.Left => "←",
        Key.Right => "→",
        Key.Up => "↑",
        Key.Down => "↓",
        Key.Return => "Enter",
        Key.Space => "Space",
        _ => key.ToString(),
    };
}
