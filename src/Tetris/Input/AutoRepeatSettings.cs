namespace Tetris.Input;

/// <summary>
/// DAS（初回リピートまでの遅延）/ARR（リピート間隔）の設定値。キーコンフィグと同様、UIでのリマップ・永続化の対象。
/// </summary>
public sealed class AutoRepeatSettings
{
    public TimeSpan Das { get; }
    public TimeSpan Arr { get; }

    private AutoRepeatSettings(TimeSpan das, TimeSpan arr)
    {
        Das = das;
        Arr = arr;
    }

    /// <summary>既定値（<see cref="AutoRepeatController.DefaultDas"/>/<see cref="AutoRepeatController.DefaultArr"/> と同一）。</summary>
    public static AutoRepeatSettings Default() => new(AutoRepeatController.DefaultDas, AutoRepeatController.DefaultArr);

    /// <summary>
    /// DAS/ARRの値を検証して生成する。
    /// ARRは0以下だと AutoRepeatController 内で0除算になるため正の値のみ許可し、DASは0以上を許可する（0 = 押した瞬間からリピート開始）。
    /// </summary>
    public static bool TryCreate(TimeSpan das, TimeSpan arr, out AutoRepeatSettings? settings, out string? error)
    {
        if (das < TimeSpan.Zero)
        {
            settings = null;
            error = "DASは0ms以上である必要があります。";
            return false;
        }
        if (arr <= TimeSpan.Zero)
        {
            settings = null;
            error = "ARRは0msより大きい値である必要があります。";
            return false;
        }
        settings = new AutoRepeatSettings(das, arr);
        error = null;
        return true;
    }
}
