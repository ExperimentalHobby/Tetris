using System.IO;
using System.Windows.Media;

namespace Tetris.Services;

/// <summary>
/// MP3 / WAV ファイルを使って効果音を再生するサービス。ファイルが存在しない場合は無音で続行する。
/// </summary>
public sealed class SoundEffectService
{
	private readonly string _dir;

	/// <summary>
	/// 音種ごとの再生プレイヤー。毎回生成すると再生中に GC で回収されうるうえ、
	/// MediaEnded が来ないケースで解放漏れになるため、ファイル名をキーに使い回す。
	/// </summary>
	private readonly Dictionary<string, MediaPlayer> _players = new();
	private double _volume = 1.0;

	/// <summary>再生音量（0.0〜1.0）。範囲外の値は自動的にクランプされる。</summary>
	public double Volume
	{
		get => _volume;
		set => _volume = Math.Clamp(value, 0.0, 1.0);
	}

	/// <summary>ミュート中かどうか。true の間は <see cref="Volume"/> に関わらず無音になる。</summary>
	public bool IsMuted { get; set; }

	/// <summary>既定の音声フォルダ（実行ファイルと同階層の Sounds/）でインスタンスを生成する。</summary>
	public SoundEffectService()
		: this(Path.Combine(AppContext.BaseDirectory, "Sounds"))
	{
	}

	/// <summary>テスト用: 音声フォルダを指定してインスタンスを生成する。</summary>
	internal SoundEffectService(string directory)
	{
		_dir = directory;
	}

	/// <summary>回転成功時の効果音を再生する。</summary>
	public void PlayRotate() => Play("rotate.mp3");

	/// <summary>ピース着地時の効果音を再生する。</summary>
	public void PlayLock() => Play("lock.mp3");

	/// <summary>ライン消去時（1〜3行）の効果音を再生する。</summary>
	public void PlayLineClear() => Play("line_clear.mp3");

	/// <summary>テトリス（4行消去）時の効果音を再生する。</summary>
	public void PlayTetris() => Play("tetris.mp3");

	/// <summary>ゲームオーバー時の効果音を再生する。</summary>
	public void PlayGameOver() => Play("game_over.mp3");

	private void Play(string fileName)
	{
		var path = Path.Combine(_dir, fileName);
		if (!File.Exists(path))
		{
			return;
		}
		try
		{
			if (!_players.TryGetValue(fileName, out var player))
			{
				player = new MediaPlayer();
				player.Open(new Uri(path, UriKind.Absolute));
				_players[fileName] = player;
			}
			else
			{
				// 同じ効果音が連続したときは重ねず、先頭から鳴らし直す。
				player.Position = TimeSpan.Zero;
			}
			player.Volume = IsMuted ? 0.0 : Volume;
			player.Play();
		}
		catch
		{
			// 再生失敗はゲームに影響させない。
		}
	}
}
