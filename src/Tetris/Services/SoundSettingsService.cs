using System.IO;
using System.Text.Json;

namespace Tetris.Services;

/// <summary>
/// 効果音設定（<see cref="SoundSettings"/>）をローカルファイルに保存・読み込みするサービス。
/// </summary>
public sealed class SoundSettingsService
{
	private readonly string _filePath;

	/// <summary>既定の保存先（%LOCALAPPDATA%\Tetris\sound.json）でインスタンスを生成する。</summary>
	public SoundSettingsService()
		: this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tetris"))
	{
	}

	/// <summary>テスト用: 保存先ディレクトリを指定してインスタンスを生成する。</summary>
	internal SoundSettingsService(string directory)
	{
		_filePath = Path.Combine(directory, "sound.json");
	}

	/// <summary>保存済みの効果音設定を返す。ファイルが無い・壊れている場合は既定値を返す。</summary>
	public SoundSettings Load()
	{
		if (!File.Exists(_filePath))
		{
			return SoundSettings.Default();
		}
		try
		{
			var json = File.ReadAllText(_filePath);
			var dto = JsonSerializer.Deserialize<Dto>(json);
			return dto is null ? SoundSettings.Default() : SoundSettings.Create(dto.Volume, dto.IsMuted);
		}
		catch
		{
			return SoundSettings.Default();
		}
	}

	/// <summary>
	/// 効果音設定をファイルに保存する。保存先ディレクトリが無ければ作成する。
	/// 保存先ディレクトリの作成やファイル書き込みに失敗しても、<see cref="Load"/> と対称的に
	/// 例外を投げず失敗を無視する（保存先の権限不足・容量不足等でアプリがクラッシュしないため）。
	/// </summary>
	public void Save(SoundSettings settings)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
			var dto = new Dto { Volume = settings.Volume, IsMuted = settings.IsMuted };
			File.WriteAllText(_filePath, JsonSerializer.Serialize(dto));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// 保存に失敗してもゲームの進行に影響させない。
		}
	}

	/// <summary>JSON 永続化用のデータ転送オブジェクト。</summary>
	private sealed class Dto
	{
		public double Volume { get; set; }
		public bool IsMuted { get; set; }
	}
}
