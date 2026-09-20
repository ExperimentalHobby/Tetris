using System.IO;
using Tetris.Services;

namespace Tetris.Tests;

/// <summary>
/// <see cref="HighScoreService"/> のファイル読み書き動作を検証するテスト。
/// </summary>
public class HighScoreServiceTests : IDisposable
{
	private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

	private HighScoreService CreateService() => new(_tempDir);

	public void Dispose()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// ファイルが存在しない初回起動時はハイスコアが 0 であることを確認する。
	/// パス条件: Load() が 0 を返す。
	/// </summary>
	[Fact]
	public void LoadWhenFileNotExistsReturnsZero()
	{
		var service = CreateService();

		var result = service.Load();

		Assert.Equal(0, result);
	}

	/// <summary>
	/// Save したスコアを Load で取得できることを確認する。
	/// パス条件: Save(500) の後 Load() が 500 を返す。
	/// </summary>
	[Fact]
	public void LoadAfterSaveReturnsSavedScore()
	{
		var service = CreateService();
		service.Save(500);

		var result = service.Load();

		Assert.Equal(500, result);
	}

	/// <summary>
	/// 複数回 Save した場合は最後の値が読み込まれることを確認する。
	/// パス条件: Save(100) → Save(800) の後 Load() が 800 を返す。
	/// </summary>
	[Fact]
	public void LoadAfterMultipleSavesReturnsLastSaved()
	{
		var service = CreateService();
		service.Save(100);
		service.Save(800);

		var result = service.Load();

		Assert.Equal(800, result);
	}

	/// <summary>
	/// 保存先ディレクトリが存在しなくても Save が成功することを確認する。
	/// パス条件: 例外を投げず、Load() が保存値を返す。
	/// </summary>
	[Fact]
	public void SaveCreatesDirectoryIfNotExists()
	{
		var service = new HighScoreService(Path.Combine(_tempDir, "nested", "dir"));
		service.Save(300);

		var result = service.Load();

		Assert.Equal(300, result);
	}

	/// <summary>
	/// 保存ファイルの中身が壊れている（JSON として解釈できない）場合、
	/// 例外を投げず 0 を返す（既定値へのフォールバック）ことを確認する。
	/// パス条件: 不正な内容のファイルを直接書いた後 Load() が 0 を返す。
	/// </summary>
	[Fact]
	public void LoadWhenFileIsCorruptedReturnsZero()
	{
		Directory.CreateDirectory(_tempDir);
		File.WriteAllText(Path.Combine(_tempDir, "highscore.json"), "{ this is not valid json");
		var service = CreateService();

		var result = service.Load();

		Assert.Equal(0, result);
	}
}
