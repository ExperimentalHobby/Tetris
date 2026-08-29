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
}
