namespace Tetris.Input;

/// <summary>
/// キーにリマップ可能なゲーム操作。
/// </summary>
public enum GameAction
{
    MoveLeft,
    MoveRight,
    Rotate,
    RotateCcw,
    SoftDrop,
    HardDrop,
    Hold,
    Start,
    Pause,
    ToggleMute,
}
