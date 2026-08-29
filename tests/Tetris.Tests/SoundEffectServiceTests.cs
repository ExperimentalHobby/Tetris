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
	public void PlayMethodsWhenFilesNotExistDoNotThrow()
	{
		var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		var service = new SoundEffectService(emptyDir);

		// ファイルが存在しなくても例外を投げないこと（ここまで到達すればパス）
		service.PlayRotate();
		service.PlayLock();
		service.PlayLineClear();
		service.PlayTetris();
	}

	/// <summary>
	/// 新規インスタンスの Volume が 1.0（最大）であることを確認する。
	/// </summary>
	[Fact]
	public void VolumeDefaultsToOne()
	{
		var service = new SoundEffectService(Path.GetTempPath());

		Assert.Equal(1.0, service.Volume);
	}

	/// <summary>
	/// Volume に範囲外の値を設定すると 0.0〜1.0 にクランプされることを確認する。
	/// </summary>
	[Fact]
	public void VolumeClampsToValidRange()
	{
		var service = new SoundEffectService(Path.GetTempPath());

		service.Volume = 1.5;
		Assert.Equal(1.0, service.Volume);

		service.Volume = -0.5;
		Assert.Equal(0.0, service.Volume);
	}

	/// <summary>
	/// 新規インスタンスの IsMuted が false（ミュートされていない）であることを確認する。
	/// </summary>
	[Fact]
	public void IsMutedDefaultsToFalse()
	{
		var service = new SoundEffectService(Path.GetTempPath());

		Assert.False(service.IsMuted);
	}

	/// <summary>
	/// 音声ファイルが存在しない環境で Volume/IsMuted を変更した後も、
	/// 各 Play メソッドが例外を投げないことを確認する。
	/// </summary>
	[Fact]
	public void PlayMethodsAfterChangingVolumeAndMuteDoNotThrow()
	{
		var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		var service = new SoundEffectService(emptyDir)
		{
			Volume = 0.3,
			IsMuted = true,
		};

		service.PlayRotate();
		service.PlayLock();
		service.PlayLineClear();
		service.PlayTetris();
		service.PlayGameOver();
	}
}
