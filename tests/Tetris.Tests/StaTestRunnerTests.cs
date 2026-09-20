using System.Threading;
using System.Windows.Threading;

namespace Tetris.Tests;

/// <summary>
/// <see cref="StaTestRunner"/> 自体の挙動（STAスレッドでの実行・例外伝播）を検証するテスト。
/// </summary>
public class StaTestRunnerTests
{
	/// <summary>
	/// パス条件: Run() に渡した処理が STA アパートメント状態のスレッド上で実行される。
	/// </summary>
	[Fact]
	public void RunExecutesActionOnStaThread()
	{
		ApartmentState? observed = null;

		StaTestRunner.Run(() =>
		{
			observed = Thread.CurrentThread.GetApartmentState();
		});

		Assert.Equal(ApartmentState.STA, observed);
	}

	/// <summary>
	/// パス条件: Run() に渡した処理が投げた例外が、呼び出し元スレッドへそのまま再スローされる。
	/// </summary>
	[Fact]
	public void RunRethrowsExceptionFromAction()
	{
		var ex = Assert.Throws<InvalidOperationException>(() =>
			StaTestRunner.Run(() => throw new InvalidOperationException("boom")));

		Assert.Equal("boom", ex.Message);
	}

	/// <summary>
	/// パス条件: RunWithMessagePump() が実際に DispatcherTimer の Tick を複数回発火させ、
	/// until() が true を返した時点で処理を終える。
	/// </summary>
	[Fact]
	public void RunWithMessagePumpProcessesRealDispatcherTimerTicks()
	{
		int tickCount = 0;

		StaTestRunner.RunWithMessagePump(
			setup: dispatcher =>
			{
				var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5) };
				timer.Tick += (_, _) => Interlocked.Increment(ref tickCount);
				timer.Start();
			},
			until: () => Volatile.Read(ref tickCount) >= 3,
			timeout: TimeSpan.FromSeconds(3));

		Assert.True(tickCount >= 3);
	}
}
