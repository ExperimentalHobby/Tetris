using System.IO;
using Tetris.Services;

namespace Tetris.Tests;

/// <summary>
/// <see cref="SoundEffectService"/> の堅牢性を検証するテスト。
/// </summary>
public class SoundEffectServiceTests
{
    /// <summary>
    /// 音声ファイルが存在しない場合でも各 Play メソッドが例外を投げないことを確認する。
    /// パス条件: 空ディレクトリを指定しても全メソッドが正常終了する。
    /// </summary>
    [Fact]
    public void PlayMethods_WhenFilesNotExist_DoNotThrow()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var service = new SoundEffectService(emptyDir);

        // ファイルが存在しなくても例外を投げないこと（ここまで到達すればパス）
        service.PlayRotate();
        service.PlayLock();
        service.PlayLineClear();
        service.PlayTetris();
    }
}
