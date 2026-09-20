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

	/// <summary>実行ファイルと同階層の実際の Sounds フォルダ（ビルド時にコピーされる）を指すディレクトリ。</summary>
	private static string RealSoundsDir => Path.Combine(AppContext.BaseDirectory, "Sounds");

	/// <summary>
	/// 実在する音声ファイルに対して Play を呼ぶと、MediaPlayer の生成・再生まで例外なく完了することを確認する。
	/// MediaPlayer は Dispatcher affinity を持つため STA スレッド上で検証する。
	/// パス条件: 実ファイルが存在する状態で PlayRotate() を呼んでも例外を投げない。
	/// </summary>
	[Fact]
	public void PlayWithExistingFileDoesNotThrow()
	{
		StaTestRunner.Run(() =>
		{
			var service = new SoundEffectService(RealSoundsDir);

			service.PlayRotate();
		});
	}

	/// <summary>
	/// 同じ効果音を連続して呼んだ場合、2回目は新規 MediaPlayer を作らず既存のものを先頭から再生し直す
	/// （内部キャッシュの再利用経路）ことを、例外が起きないことで確認する。
	/// パス条件: 同じ Play メソッドを2回連続で呼んでも例外を投げない。
	/// </summary>
	[Fact]
	public void PlayingSameSoundTwiceReusesCachedPlayerWithoutThrowing()
	{
		StaTestRunner.Run(() =>
		{
			var service = new SoundEffectService(RealSoundsDir);

			service.PlayRotate();
			service.PlayRotate();
		});
	}

	/// <summary>
	/// ミュート中に実ファイルを再生しても例外を投げないことを確認する（Volume = 0 で再生される経路）。
	/// パス条件: IsMuted = true の状態で PlayLock() を呼んでも例外を投げない。
	/// </summary>
	[Fact]
	public void PlayWhileMutedDoesNotThrow()
	{
		StaTestRunner.Run(() =>
		{
			var service = new SoundEffectService(RealSoundsDir) { IsMuted = true };

			service.PlayLock();
		});
	}
}
