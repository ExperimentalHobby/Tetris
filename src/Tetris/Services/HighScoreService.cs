using System.IO;
using System.Text.Json;

namespace Tetris.Services;

/// <summary>
/// ハイスコアをローカルファイルに保存・読み込みするサービス。
/// </summary>
public sealed class HighScoreService
{
    private readonly string _filePath;

    /// <summary>既定の保存先（%LOCALAPPDATA%\Tetris\highscore.json）でインスタンスを生成する。</summary>
    public HighScoreService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tetris"))
    {
    }

    /// <summary>テスト用: 保存先ディレクトリを指定してインスタンスを生成する。</summary>
    internal HighScoreService(string directory)
    {
        _filePath = Path.Combine(directory, "highscore.json");
    }

    /// <summary>保存済みのハイスコアを返す。ファイルが存在しない場合は 0 を返す。</summary>
    public int Load()
    {
        if (!File.Exists(_filePath))
        {
            return 0;
        }
        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<int>(json);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>ハイスコアをファイルに保存する。保存先ディレクトリが無ければ作成する。</summary>
    public void Save(int score)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(score));
    }
}
