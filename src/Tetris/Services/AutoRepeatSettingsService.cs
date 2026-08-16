using System.IO;
using System.Text.Json;
using Tetris.Input;

namespace Tetris.Services;

/// <summary>
/// DAS/ARR設定（<see cref="AutoRepeatSettings"/>）をローカルファイルに保存・読み込みするサービス。
/// </summary>
public sealed class AutoRepeatSettingsService
{
    private readonly string _filePath;

    /// <summary>既定の保存先（%LOCALAPPDATA%\Tetris\autorepeat.json）でインスタンスを生成する。</summary>
    public AutoRepeatSettingsService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tetris"))
    {
    }

    /// <summary>テスト用: 保存先ディレクトリを指定してインスタンスを生成する。</summary>
    internal AutoRepeatSettingsService(string directory)
    {
        _filePath = Path.Combine(directory, "autorepeat.json");
    }

    /// <summary>保存済みのDAS/ARR設定を返す。ファイルが無い・壊れている・不正な値の場合は既定値を返す。</summary>
    public AutoRepeatSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return AutoRepeatSettings.Default();
        }
        try
        {
            var json = File.ReadAllText(_filePath);
            var dto = JsonSerializer.Deserialize<Dto>(json);
            if (dto is null)
            {
                return AutoRepeatSettings.Default();
            }
            if (AutoRepeatSettings.TryCreate(
                    TimeSpan.FromMilliseconds(dto.DasMs), TimeSpan.FromMilliseconds(dto.ArrMs),
                    out var settings, out _))
            {
                return settings!;
            }
            return AutoRepeatSettings.Default();
        }
        catch
        {
            return AutoRepeatSettings.Default();
        }
    }

    /// <summary>DAS/ARR設定をファイルに保存する。保存先ディレクトリが無ければ作成する。</summary>
    public void Save(AutoRepeatSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var dto = new Dto { DasMs = settings.Das.TotalMilliseconds, ArrMs = settings.Arr.TotalMilliseconds };
        File.WriteAllText(_filePath, JsonSerializer.Serialize(dto));
    }

    /// <summary>JSON永続化用のデータ転送オブジェクト。TimeSpanを直接シリアライズせずms単位のdoubleにする。</summary>
    private sealed class Dto
    {
        public double DasMs { get; set; }
        public double ArrMs { get; set; }
    }
}
