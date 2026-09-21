namespace Tetris.Tests;

/// <summary>
/// MainWindow を生成するテストクラスを直列実行にするための xUnit コレクション定義。
/// MainWindow.GetFillBrush が使う static な FillBrushes（Dictionary）はスレッドセーフでなく、
/// 複数の MainWindow インスタンスを異なるスレッドで同時に生成・描画すると
/// 「コレクションが壊れた」例外で不安定になる（実プロダクトでは MainWindow は常に1つだけなので
/// 顕在化しない潜在バグ。詳細は別途 Issue 化する）。
/// テスト側の回避として、このコレクションに属するテストクラス間の並列実行を無効化する。
/// </summary>
[CollectionDefinition("MainWindow", DisableParallelization = true)]
public class MainWindowTestGroup
{
}
