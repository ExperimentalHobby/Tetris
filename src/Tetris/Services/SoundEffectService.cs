using System.IO;
using System.Windows.Media;

namespace Tetris.Services;

/// <summary>
/// MP3 / WAV ファイルを使って効果音を再生するサービス。ファイルが存在しない場合は無音で続行する。
/// </summary>
public sealed class SoundEffectService
{
    private readonly string _dir;

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
            var player = new MediaPlayer();
            // 再生終了後に自動でリソースを解放する。
            player.MediaEnded += (s, _) => ((MediaPlayer)s!).Close();
            player.Open(new Uri(path, UriKind.Absolute));
            player.Play();
        }
        catch
        {
            // 再生失敗はゲームに影響させない。
        }
    }
}
