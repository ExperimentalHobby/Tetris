namespace Tetris.Services;

/// <summary>
/// 効果音の音量・ミュート設定。永続化（<see cref="SoundSettingsService"/>）の対象。
/// </summary>
public sealed class SoundSettings
{
	/// <summary>再生音量（0.0〜1.0）。</summary>
	public double Volume { get; }

	/// <summary>ミュート中かどうか。</summary>
	public bool IsMuted { get; }

	private SoundSettings(double volume, bool isMuted)
	{
		Volume = volume;
		IsMuted = isMuted;
	}

	/// <summary>既定値（音量 1.0 / ミュートなし）。</summary>
	public static SoundSettings Default() => new(1.0, false);

	/// <summary>
	/// 音量を 0.0〜1.0 にクランプして生成する。NaN や無限大は既定の音量（1.0）にフォールバックする
	/// （保存ファイルが壊れていても再生時に例外にならないようにするため）。
	/// </summary>
	public static SoundSettings Create(double volume, bool isMuted)
	{
		double safeVolume = double.IsFinite(volume) ? Math.Clamp(volume, 0.0, 1.0) : 1.0;
		return new SoundSettings(safeVolume, isMuted);
	}
}
