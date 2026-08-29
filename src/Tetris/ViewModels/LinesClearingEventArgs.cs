namespace Tetris.ViewModels;

/// <summary>
/// ライン消去アニメーションの開始を View に伝えるためのイベント引数。
/// </summary>
public sealed class LinesClearingEventArgs : EventArgs
{
	public LinesClearingEventArgs(IReadOnlyList<int> rows)
	{
		Rows = rows;
	}

	/// <summary>消去対象の行番号（盤面の行インデックス）。</summary>
	public IReadOnlyList<int> Rows { get; }
}
