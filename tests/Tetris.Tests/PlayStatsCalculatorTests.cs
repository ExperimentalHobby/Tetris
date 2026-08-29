using Tetris.ViewModels;

namespace Tetris.Tests;

/// <summary>
/// <see cref="PlayStatsCalculator"/> のPPS/LPM算出ロジックを検証するテスト。
/// </summary>
public class PlayStatsCalculatorTests
{
	/// <summary>
	/// 経過時間が0のときはPPSが0になることを確認する（0除算の防止）。
	/// パス条件: PiecesPerSecond(10, TimeSpan.Zero) が 0。
	/// </summary>
	[Fact]
	public void PiecesPerSecondWithZeroElapsedReturnsZero()
	{
		double pps = PlayStatsCalculator.PiecesPerSecond(10, TimeSpan.Zero);

		Assert.Equal(0, pps);
	}

	/// <summary>
	/// ピース数と経過時間から正しいPPSが算出されることを確認する。
	/// パス条件: 10ピース/5秒 で PPS=2.0。
	/// </summary>
	[Fact]
	public void PiecesPerSecondWithElapsedTimeReturnsCorrectRate()
	{
		double pps = PlayStatsCalculator.PiecesPerSecond(10, TimeSpan.FromSeconds(5));

		Assert.Equal(2.0, pps);
	}

	/// <summary>
	/// 経過時間が0のときはLPMが0になることを確認する（0除算の防止）。
	/// パス条件: LinesPerMinute(6, TimeSpan.Zero) が 0。
	/// </summary>
	[Fact]
	public void LinesPerMinuteWithZeroElapsedReturnsZero()
	{
		double lpm = PlayStatsCalculator.LinesPerMinute(6, TimeSpan.Zero);

		Assert.Equal(0, lpm);
	}

	/// <summary>
	/// ライン数と経過時間から正しいLPMが算出されることを確認する。
	/// パス条件: 6ライン/30秒 で LPM=12.0（1分あたり換算）。
	/// </summary>
	[Fact]
	public void LinesPerMinuteWithElapsedTimeReturnsCorrectRate()
	{
		double lpm = PlayStatsCalculator.LinesPerMinute(6, TimeSpan.FromSeconds(30));

		Assert.Equal(12.0, lpm);
	}
}
