namespace Tetris.ViewModels;

/// <summary>
/// プレイ中のライブ統計（PPS/LPM）を算出する。WPF に依存しない純粋な計算ロジック。
/// </summary>
internal static class PlayStatsCalculator
{
    /// <summary>PPS（Pieces Per Second）を算出する。経過時間が0以下なら0除算を避けて0を返す。</summary>
    public static double PiecesPerSecond(int pieceCount, TimeSpan elapsed)
        => elapsed.TotalSeconds > 0 ? pieceCount / elapsed.TotalSeconds : 0;

    /// <summary>LPM（Lines Per Minute）を算出する。経過時間が0以下なら0除算を避けて0を返す。</summary>
    public static double LinesPerMinute(int lines, TimeSpan elapsed)
        => elapsed.TotalMinutes > 0 ? lines / elapsed.TotalMinutes : 0;
}
