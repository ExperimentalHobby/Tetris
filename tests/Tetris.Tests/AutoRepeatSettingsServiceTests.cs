using System.IO;
using Tetris.Input;
using Tetris.Services;

namespace Tetris.Tests;

/// <summary>
/// <see cref="AutoRepeatSettingsService"/> のファイル読み書き動作を検証するテスト。
/// </summary>
public class AutoRepeatSettingsServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    private AutoRepeatSettingsService CreateService() => new(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// ファイルが存在しない初回起動時は既定のDAS/ARRが返ることを確認する。
    /// パス条件: Load() の Das が AutoRepeatController.DefaultDas。
    /// </summary>
    [Fact]
    public void LoadWhenFileNotExistsReturnsDefaultSettings()
    {
        var service = CreateService();

        var result = service.Load();

        Assert.Equal(AutoRepeatController.DefaultDas, result.Das);
    }

    /// <summary>
    /// Save した設定を Load で取得できることを確認する。
    /// パス条件: Das=200ms/Arr=30ms を Save した後、Load() が同じ値を返す。
    /// </summary>
    [Fact]
    public void LoadAfterSaveReturnsSavedSettings()
    {
        var service = CreateService();
        AutoRepeatSettings.TryCreate(TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(30), out var settings, out _);
        service.Save(settings!);

        var result = service.Load();

        Assert.Equal(TimeSpan.FromMilliseconds(200), result.Das);
        Assert.Equal(TimeSpan.FromMilliseconds(30), result.Arr);
    }
}
