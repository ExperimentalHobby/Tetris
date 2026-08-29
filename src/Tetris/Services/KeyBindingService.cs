using System.IO;
using System.Text.Json;
using System.Windows.Input;
using Tetris.Input;

namespace Tetris.Services;

/// <summary>
/// キーコンフィグ（<see cref="KeyBindings"/>）をローカルファイルに保存・読み込みするサービス。
/// </summary>
public sealed class KeyBindingService
{
	private readonly string _filePath;

	/// <summary>既定の保存先（%LOCALAPPDATA%\Tetris\keybindings.json）でインスタンスを生成する。</summary>
	public KeyBindingService()
		: this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tetris"))
	{
	}

	/// <summary>テスト用: 保存先ディレクトリを指定してインスタンスを生成する。</summary>
	internal KeyBindingService(string directory)
	{
		_filePath = Path.Combine(directory, "keybindings.json");
	}

	/// <summary>保存済みのキーコンフィグを返す。ファイルが無い・壊れている場合は既定値を返す。</summary>
	public KeyBindings Load()
	{
		if (!File.Exists(_filePath))
		{
			return KeyBindings.Default();
		}
		try
		{
			var json = File.ReadAllText(_filePath);
			var saved = JsonSerializer.Deserialize<Dictionary<GameAction, Key>>(json);
			return saved is null ? KeyBindings.Default() : KeyBindings.FromSaved(saved);
		}
		catch
		{
			return KeyBindings.Default();
		}
	}

	/// <summary>キーコンフィグをファイルに保存する。保存先ディレクトリが無ければ作成する。</summary>
	public void Save(KeyBindings bindings)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
		File.WriteAllText(_filePath, JsonSerializer.Serialize(bindings.ToDictionary()));
	}
}
