namespace Tetris.Tests;

/// <summary>
/// MainWindow を生成するテストクラスを直列実行にするための xUnit コレクション定義。
///
/// 当初は MainWindow.GetFillBrush が使う static な FillBrushes（Dictionary）の
/// スレッド非セーフさが原因と考え Issue化した（#112）。GetFillBrush は
/// ConcurrentDictionary化して修正済みだが、それでもなお複数の MainWindow を
/// 異なるスレッドで同時に生成すると、WPF 自身の Application.LoadComponent
/// （XAMLリソースの読み込み）内部の System.IO.Packaging.PackagePart が
/// スレッドセーフでないことに起因する例外（NullReferenceException 等）で
/// 稀に不安定になることが判明した。これは本プロジェクトのコードではなく
/// WPF ランタイム側の制約であり、アプリケーションコード側では修正できない。
///
/// 実際のアプリでは MainWindow は常に1つ・UIスレッドも1つだけなので、
/// この制約が問題になることはない。テスト側の回避として、このコレクションに
/// 属するテストクラス間の並列実行を無効化し、MainWindow の生成・破棄が
/// 複数スレッドで重ならないようにする。
/// </summary>
[CollectionDefinition("MainWindow", DisableParallelization = true)]
public class MainWindowTestGroup
{
}
