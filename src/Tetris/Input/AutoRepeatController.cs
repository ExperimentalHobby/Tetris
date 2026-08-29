namespace Tetris.Input;

/// <summary>
/// キー押しっぱなし時の DAS（初回リピートまでの遅延）と ARR（リピート間隔）を管理する。
/// WPF などの入力基盤から独立しており、経過時間を渡してリピート回数を受け取る形で利用する。
/// </summary>
public sealed class AutoRepeatController
{
	public static readonly TimeSpan DefaultDas = TimeSpan.FromMilliseconds(170);
	public static readonly TimeSpan DefaultArr = TimeSpan.FromMilliseconds(50);

	private readonly TimeSpan _das;
	private readonly TimeSpan _arr;

	private bool _isDown;
	private bool _hasStartedRepeating;
	private TimeSpan _elapsedSinceDown;

	public AutoRepeatController(TimeSpan? das = null, TimeSpan? arr = null)
	{
		_das = das ?? DefaultDas;
		_arr = arr ?? DefaultArr;
		if (_arr <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(arr), _arr, "ARR は正の値である必要があります（0以下だと Advance 内の除算で 0 除算になるため）。");
		}
	}

	/// <summary>キーが押された瞬間に呼ぶ。リピート状態を初期化する。</summary>
	public void KeyDown()
	{
		_isDown = true;
		_hasStartedRepeating = false;
		_elapsedSinceDown = TimeSpan.Zero;
	}

	/// <summary>キーが離された瞬間に呼ぶ。以後 Advance は 0 を返す。</summary>
	public void KeyUp()
	{
		_isDown = false;
		_hasStartedRepeating = false;
		_elapsedSinceDown = TimeSpan.Zero;
	}

	/// <summary>経過時間を進め、この間に発生させるべきリピート回数を返す。</summary>
	public int Advance(TimeSpan elapsed)
	{
		if (!_isDown)
		{
			return 0;
		}

		_elapsedSinceDown += elapsed;

		if (!_hasStartedRepeating)
		{
			if (_elapsedSinceDown < _das)
			{
				return 0;
			}
			_hasStartedRepeating = true;
			var overflow = _elapsedSinceDown - _das;
			int firstRepeats = 1 + (int)(overflow / _arr);
			_elapsedSinceDown = TimeSpan.FromTicks(overflow.Ticks % _arr.Ticks);
			return firstRepeats;
		}

		int repeats = (int)(_elapsedSinceDown / _arr);
		_elapsedSinceDown = TimeSpan.FromTicks(_elapsedSinceDown.Ticks % _arr.Ticks);
		return repeats;
	}
}
