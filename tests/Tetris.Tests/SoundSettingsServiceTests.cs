using System.IO;
using Tetris.Services;

namespace Tetris.Tests;

/// <summary>
/// <see cref="SoundSettingsService"/> の音量・ミュート設定の永続化を検証するテスト。
/// 実ファイルへの副作用を避けるため、一時ディレクトリを指定して生成する。
/// </summary>
public class SoundSettingsServiceTests : IDisposable
{
	private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

	private SoundSettingsService CreateService() => new(_tempDir);

	public void Dispose()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// 保存ファイルが無いときは既定値（音量 1.0 / ミュート false）を返すことを確認する。
	/// パス条件: Load() の Volume が 1.0、IsMuted が false。
	/// </summary>
	[Fact]
	public void LoadWhenFileNotExistsReturnsDefault()
	{
		var service = CreateService();

		var settings = service.Load();

		Assert.Equal(1.0, settings.Volume);
		Assert.False(settings.IsMuted);
	}

	/// <summary>
	/// 保存した設定が読み込みで往復することを確認する。
	/// パス条件: Save(0.35, true) の後 Load() が同じ値を返す。
	/// </summary>
	[Fact]
	public void LoadAfterSaveReturnsSavedSettings()
	{
		var service = CreateService();
		service.Save(SoundSettings.Create(0.35, isMuted: true));

		var settings = service.Load();

		Assert.Equal(0.35, settings.Volume);
		Assert.True(settings.IsMuted);
	}

	/// <summary>
	/// 保存ファイルが壊れている場合に既定値へフォールバックすることを確認する。
	/// パス条件: JSON として不正な内容を書いても例外にならず、Load() が既定値を返す。
	/// </summary>
	[Fact]
	public void LoadWhenFileIsCorruptReturnsDefault()
	{
		Directory.CreateDirectory(_tempDir);
		File.WriteAllText(Path.Combine(_tempDir, "sound.json"), "{ this is not valid json");
		var service = CreateService();

		var settings = service.Load();

		Assert.Equal(1.0, settings.Volume);
		Assert.False(settings.IsMuted);
	}

	/// <summary>
	/// 範囲外・非有限の音量が安全な値に補正されて読み込まれることを確認する。
	/// パス条件: 保存ファイルに 5.0 / -1.0 / 無限大が入っていても Load() の Volume が 0.0〜1.0 に収まる。
	/// </summary>
	[Theory]
	[InlineData("5.0", 1.0)]
	[InlineData("-1.0", 0.0)]
	[InlineData("1e309", 1.0)]  // 無限大相当。既定音量へフォールバックする
	public void LoadClampsOutOfRangeVolume(string rawVolume, double expected)
	{
		Directory.CreateDirectory(_tempDir);
		File.WriteAllText(
			Path.Combine(_tempDir, "sound.json"),
			"{\"Volume\":" + rawVolume + ",\"IsMuted\":false}");
		var service = CreateService();

		var settings = service.Load();

		Assert.Equal(expected, settings.Volume);
	}
}
