using Tetris.Input;

namespace Tetris.Tests;

/// <summary>
/// <see cref="AutoRepeatSettings"/> の既定値・検証ロジックを検証するテスト。
/// </summary>
public class AutoRepeatSettingsTests
{
    /// <summary>
    /// 既定値が AutoRepeatController の既定値と一致することを確認する。
    /// パス条件: Default().Das/Arr が AutoRepeatController.DefaultDas/DefaultArr と等しい。
    /// </summary>
    [Fact]
    public void Default_MatchesAutoRepeatControllerDefaults()
    {
        var settings = AutoRepeatSettings.Default();

        Assert.Equal(AutoRepeatController.DefaultDas, settings.Das);
        Assert.Equal(AutoRepeatController.DefaultArr, settings.Arr);
    }

    /// <summary>
    /// 正当な値であれば生成に成功し、指定した値がそのまま反映されることを確認する。
    /// パス条件: TryCreate(200ms, 30ms) が true を返し、settings.Das/Arr がその値になる。
    /// </summary>
    [Fact]
    public void TryCreate_WithValidValues_ReturnsTrueAndSettings()
    {
        bool result = AutoRepeatSettings.TryCreate(
            TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(30),
            out var settings, out var error);

        Assert.True(result);
        Assert.Equal(TimeSpan.FromMilliseconds(200), settings!.Das);
        Assert.Equal(TimeSpan.FromMilliseconds(30), settings.Arr);
        Assert.Null(error);
    }

    /// <summary>
    /// ARRに0を指定すると失敗し、エラーメッセージが返ることを確認する（Issue #24: 0除算の防止）。
    /// パス条件: TryCreate(200ms, 0ms) が false を返し、settings は null、error は非null。
    /// </summary>
    [Fact]
    public void TryCreate_WithZeroArr_ReturnsFalseWithError()
    {
        bool result = AutoRepeatSettings.TryCreate(
            TimeSpan.FromMilliseconds(200), TimeSpan.Zero,
            out var settings, out var error);

        Assert.False(result);
        Assert.Null(settings);
        Assert.NotNull(error);
    }

    /// <summary>
    /// DASに負の値を指定すると失敗し、エラーメッセージが返ることを確認する。
    /// パス条件: TryCreate(-1ms, 30ms) が false を返し、settings は null、error は非null。
    /// </summary>
    [Fact]
    public void TryCreate_WithNegativeDas_ReturnsFalseWithError()
    {
        bool result = AutoRepeatSettings.TryCreate(
            TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(30),
            out var settings, out var error);

        Assert.False(result);
        Assert.Null(settings);
        Assert.NotNull(error);
    }
}
