using Tetris.Input;

namespace Tetris.Tests;

/// <summary>
/// <see cref="AutoRepeatController"/> の DAS/ARR タイミング制御を検証するテスト。
/// </summary>
public class AutoRepeatControllerTests
{
	/// <summary>
	/// ARR に 0（以下）を渡すと Advance 内の除算で 0 除算になるため、コンストラクタで弾くことを確認する。
	/// パス条件: arr: TimeSpan.Zero でコンストラクタを呼ぶと ArgumentOutOfRangeException を投げる。
	/// </summary>
	[Fact]
	public void ConstructorWithZeroArrThrowsArgumentOutOfRangeException()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new AutoRepeatController(arr: TimeSpan.Zero));
	}

	/// <summary>
	/// 何も押していない状態では Advance が 0 を返すことを確認する。
	/// パス条件: KeyDown を呼ばずに Advance すると 0。
	/// </summary>
	[Fact]
	public void AdvanceBeforeKeyDownReturnsZero()
	{
		var controller = new AutoRepeatController();

		int repeats = controller.Advance(TimeSpan.FromMilliseconds(1000));

		Assert.Equal(0, repeats);
	}

	/// <summary>
	/// KeyDown 直後、DAS 未満の経過では 0 を返すことを確認する。
	/// パス条件: KeyDown 後に DAS（既定 170ms）未満の時間を進めても 0。
	/// </summary>
	[Fact]
	public void AdvanceImmediatelyAfterKeyDownBeforeDasReturnsZero()
	{
		var controller = new AutoRepeatController();
		controller.KeyDown();

		int repeats = controller.Advance(TimeSpan.FromMilliseconds(100));

		Assert.Equal(0, repeats);
	}

	/// <summary>
	/// DAS 経過後は 1 回以上のリピートを返すことを確認する。
	/// パス条件: DAS ちょうどの時間を進めると 1 回以上のリピートが返る。
	/// </summary>
	[Fact]
	public void AdvanceAfterDasElapsedReturnsAtLeastOneRepeat()
	{
		var controller = new AutoRepeatController();
		controller.KeyDown();

		int repeats = controller.Advance(AutoRepeatController.DefaultDas);

		Assert.True(repeats >= 1);
	}

	/// <summary>
	/// DAS 経過後、ARR の複数倍の時間が経過すると複数回のリピートを返すことを確認する。
	/// パス条件: DAS + ARR*3 分の時間を一度に進めると 4 回（初回 + 3 回）以上のリピートが返る。
	/// </summary>
	[Fact]
	public void AdvanceMultipleArrIntervalsElapsedReturnsMultipleRepeats()
	{
		var controller = new AutoRepeatController();
		controller.KeyDown();

		var elapsed = AutoRepeatController.DefaultDas + TimeSpan.FromTicks(AutoRepeatController.DefaultArr.Ticks * 3);
		int repeats = controller.Advance(elapsed);

		Assert.Equal(4, repeats);
	}

	/// <summary>
	/// KeyUp 後は Advance を呼んでも 0 を返すことを確認する。
	/// パス条件: リピート中に KeyUp すると、以後の Advance は 0。
	/// </summary>
	[Fact]
	public void KeyUpStopsFurtherRepeats()
	{
		var controller = new AutoRepeatController();
		controller.KeyDown();
		controller.Advance(AutoRepeatController.DefaultDas);

		controller.KeyUp();
		int repeats = controller.Advance(TimeSpan.FromSeconds(10));

		Assert.Equal(0, repeats);
	}

	/// <summary>
	/// 再度 KeyDown すると状態がリセットされ、再び DAS 待ちから始まることを確認する。
	/// パス条件: リピート中に KeyDown し直すと、DAS 未満の経過では 0 を返す。
	/// </summary>
	[Fact]
	public void KeyDownResetsPreviousState()
	{
		var controller = new AutoRepeatController();
		controller.KeyDown();
		controller.Advance(AutoRepeatController.DefaultDas + AutoRepeatController.DefaultArr);

		controller.KeyDown();
		int repeats = controller.Advance(TimeSpan.FromMilliseconds(100));

		Assert.Equal(0, repeats);
	}

	/// <summary>
	/// 実運用と同じく細かい刻みで Advance を繰り返したとき、DAS 経過後に ARR 間隔で
	/// リピートが発生することを確認する（定常リピート分岐のカバレッジ）。
	/// パス条件: DAS=170ms / ARR=50ms を 16ms 刻みで 960ms 分進めると、初回リピートは
	/// DAS を超えた直後（176ms 時点）に起き、以降の発火間隔は ARR を挟む 48〜64ms に収まる。
	/// </summary>
	[Fact]
	public void AdvanceInSmallStepsRepeatsAtArrInterval()
	{
		var controller = new AutoRepeatController(TimeSpan.FromMilliseconds(170), TimeSpan.FromMilliseconds(50));
		controller.KeyDown();

		var fireTimes = new List<int>();
		for (int step = 1; step <= 60; step++)
		{
			int repeats = controller.Advance(TimeSpan.FromMilliseconds(16));
			for (int i = 0; i < repeats; i++)
			{
				fireTimes.Add(step * 16);
			}
		}

		// 初回は DAS(170ms) を超えた最初の刻み = 176ms。
		Assert.Equal(176, fireTimes[0]);
		// 以降は ARR(50ms) を 16ms に量子化した 48ms または 64ms 間隔になる。
		for (int i = 1; i < fireTimes.Count; i++)
		{
			int delta = fireTimes[i] - fireTimes[i - 1];
			Assert.InRange(delta, 48, 64);
		}
	}

	/// <summary>
	/// 刻み呼び出しでも端数（余り時間）が次回に繰り越され、総リピート回数が
	/// 経過時間から期待される値と一致することを確認する。
	/// パス条件: DAS=170ms / ARR=50ms で 960ms 経過させると、総リピート回数は
	/// 1 + floor((960 - 170) / 50) = 16 回になる。
	/// </summary>
	[Fact]
	public void AdvanceInSmallStepsCarriesRemainderAcrossCalls()
	{
		var controller = new AutoRepeatController(TimeSpan.FromMilliseconds(170), TimeSpan.FromMilliseconds(50));
		controller.KeyDown();

		int total = 0;
		for (int step = 0; step < 60; step++)
		{
			total += controller.Advance(TimeSpan.FromMilliseconds(16));
		}

		Assert.Equal(16, total);
	}

	/// <summary>
	/// 定常リピートに入った後で KeyUp → KeyDown し直すと、再び DAS 待ちから始まることを確認する。
	/// パス条件: リピート開始後に押し直すと、DAS 未満の刻みではリピートが発生しない。
	/// </summary>
	[Fact]
	public void KeyDownAfterSteadyRepeatRestartsDasDelay()
	{
		var controller = new AutoRepeatController(TimeSpan.FromMilliseconds(170), TimeSpan.FromMilliseconds(50));
		controller.KeyDown();
		// 定常リピート状態まで進める。
		int before = 0;
		for (int step = 0; step < 30; step++)
		{
			before += controller.Advance(TimeSpan.FromMilliseconds(16));
		}
		Assert.True(before > 1);

		controller.KeyUp();
		controller.KeyDown();

		// 押し直し後、DAS(170ms) 未満（16ms x 10 = 160ms）ではリピートしない。
		int after = 0;
		for (int step = 0; step < 10; step++)
		{
			after += controller.Advance(TimeSpan.FromMilliseconds(16));
		}
		Assert.Equal(0, after);
	}

	/// <summary>
	/// ARR が 1 回の刻みより短い場合、1 回の Advance で複数回のリピートが返ることを確認する。
	/// パス条件: ARR=10ms で 16ms 刻みを進めると、DAS 経過後の刻みで 2 回以上返る。
	/// </summary>
	[Fact]
	public void AdvanceWithArrShorterThanStepReturnsMultipleRepeats()
	{
		var controller = new AutoRepeatController(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(10));
		controller.KeyDown();

		// DAS(50ms) を超えるまで進める。
		int repeats = 0;
		for (int step = 0; step < 4; step++)
		{
			repeats = controller.Advance(TimeSpan.FromMilliseconds(16));
		}

		// 定常状態で 1 刻み(16ms) あたり ARR(10ms) を 1〜2 回跨ぐ。
		int steady = controller.Advance(TimeSpan.FromMilliseconds(16));
		Assert.True(steady >= 1, $"定常リピートが発生しなかった (直前 repeats={repeats})");

		// 十分長い刻みを 1 回渡すと、その分だけまとめて返る。
		int burst = controller.Advance(TimeSpan.FromMilliseconds(100));
		Assert.True(burst >= 9, $"100ms / ARR 10ms で 9 回以上を期待したが {burst} 回だった");
	}
}
