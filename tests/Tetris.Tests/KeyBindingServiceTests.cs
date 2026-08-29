using System.IO;
using System.Windows.Input;
using Tetris.Input;
using Tetris.Services;

namespace Tetris.Tests;

/// <summary>
/// <see cref="KeyBindingService"/> のファイル読み書き動作を検証するテスト。
/// </summary>
public class KeyBindingServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    private KeyBindingService CreateService() => new(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// ファイルが存在しない初回起動時は既定のキー割り当てが返ることを確認する。
    /// パス条件: Load() の Rotate が既定値 Key.Up。
    /// </summary>
    [Fact]
    public void LoadWhenFileNotExistsReturnsDefaultBindings()
    {
        var service = CreateService();

        var result = service.Load();

        Assert.Equal(Key.Up, result.GetKey(GameAction.Rotate));
    }

    /// <summary>
    /// Save したキー割り当てを Load で取得できることを確認する。
    /// パス条件: Rotate を Key.X に変更して Save した後、Load() の Rotate が Key.X。
    /// </summary>
    [Fact]
    public void LoadAfterSaveReturnsSavedBindings()
    {
        var service = CreateService();
        var bindings = KeyBindings.Default();
        bindings.TrySetKey(GameAction.Rotate, Key.X);
        service.Save(bindings);

        var result = service.Load();

        Assert.Equal(Key.X, result.GetKey(GameAction.Rotate));
    }
}
