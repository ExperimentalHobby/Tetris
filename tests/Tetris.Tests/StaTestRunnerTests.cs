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

	/// <summary>
	/// パス条件: afterPump は until() が true になった直後、同じ STA スレッド上（Dispatcher 終了前）で実行される。
	/// WPF の DependencyObject はスレッドアフィニティを持ち、生成したスレッドが終了すると
	/// 別スレッドから触れなくなる（VerifyAccess で例外になる）ため、最終確認はここで行う必要がある。
	/// </summary>
	[Fact]
	public void RunWithMessagePumpRunsAfterPumpOnSameStaThreadBeforeExit()
	{
		ApartmentState? setupThreadApartment = null;
		ApartmentState? afterPumpThreadApartment = null;

		StaTestRunner.RunWithMessagePump(
			setup: _ => setupThreadApartment = Thread.CurrentThread.GetApartmentState(),
			until: () => true,
			timeout: TimeSpan.FromSeconds(1),
			afterPump: () => afterPumpThreadApartment = Thread.CurrentThread.GetApartmentState());

		Assert.Equal(ApartmentState.STA, setupThreadApartment);
		Assert.Equal(ApartmentState.STA, afterPumpThreadApartment);
	}
}
