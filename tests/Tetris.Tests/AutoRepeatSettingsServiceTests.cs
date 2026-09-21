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

	/// <summary>
	/// 保存ファイルの中身が JSON として解釈できない場合、例外を投げず既定値を返すことを確認する。
	/// パス条件: 不正な内容のファイルを直接書いた後 Load() が DefaultDas を返す。
	/// </summary>
	[Fact]
	public void LoadWhenFileIsCorruptedReturnsDefaultSettings()
	{
		Directory.CreateDirectory(_tempDir);
		File.WriteAllText(Path.Combine(_tempDir, "autorepeat.json"), "{ this is not valid json");
		var service = CreateService();

		var result = service.Load();

		Assert.Equal(AutoRepeatController.DefaultDas, result.Das);
	}

	/// <summary>
	/// 保存ファイルの中身が JSON としては正しいが null（"null" というJSON値）の場合、
	/// 例外を投げず既定値を返すことを確認する。
	/// パス条件: ファイルの内容が "null" のとき Load() が DefaultDas を返す。
	/// </summary>
	[Fact]
	public void LoadWhenFileContentIsJsonNullReturnsDefaultSettings()
	{
		Directory.CreateDirectory(_tempDir);
		File.WriteAllText(Path.Combine(_tempDir, "autorepeat.json"), "null");
		var service = CreateService();

		var result = service.Load();

		Assert.Equal(AutoRepeatController.DefaultDas, result.Das);
	}

	/// <summary>
	/// 保存ファイルの値が不正（DAS が負の値）で AutoRepeatSettings.TryCreate が失敗する場合、
	/// 例外を投げず既定値を返すことを確認する。
	/// パス条件: DasMs に負の値を書き込んだ後 Load() が DefaultDas を返す。
	/// </summary>
	[Fact]
	public void LoadWhenSavedValueIsInvalidReturnsDefaultSettings()
	{
		Directory.CreateDirectory(_tempDir);
		File.WriteAllText(Path.Combine(_tempDir, "autorepeat.json"), "{\"DasMs\":-1,\"ArrMs\":30}");
		var service = CreateService();

		var result = service.Load();

		Assert.Equal(AutoRepeatController.DefaultDas, result.Das);
	}

	/// <summary>
	/// 保存先ディレクトリの代わりに同名のファイルが存在し Directory.CreateDirectory が失敗する場合でも、
	/// Save() が例外を投げないことを確認する（Load() と対称的な堅牢性、Issue #104）。
	/// パス条件: 保存先ディレクトリと同名のファイルを作っておいても Save() が例外を投げない。
	/// </summary>
	[Fact]
	public void SaveWhenDirectoryCannotBeCreatedDoesNotThrow()
	{
		var blockedDir = Path.Combine(_tempDir, "blocked");
		Directory.CreateDirectory(_tempDir);
		File.WriteAllBytes(blockedDir, Array.Empty<byte>());
		var service = new AutoRepeatSettingsService(blockedDir);

		var ex = Record.Exception(() => service.Save(AutoRepeatSettings.Default()));

		Assert.Null(ex);
	}
}
