using System.IO;
using Tetris.Services;
using Tetris.ViewModels;

namespace Tetris.Tests;

/// <summary>
/// <see cref="GameViewModel.StartKeyLabel"/> による、開始/リスタート案内文のキーコンフィグ追随を
/// 検証するテスト（Issue #105）。
/// </summary>
public class GameViewModelStartKeyLabelTests : IDisposable
{
	private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

	private GameViewModel CreateViewModel() => new(
		new HighScoreService(_tempDir),
		new AutoRepeatSettingsService(_tempDir),
		new SoundEffectService(_tempDir),
		new SoundSettingsService(_tempDir));

	public void Dispose()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
		GC.SuppressFinalize(this);
	}

	/// <summary>パス条件: 既定では StartKeyLabel が "Enter"、開始前の案内文が "Enter で開始"。</summary>
	[Fact]
	public void DefaultStartKeyLabelIsEnter()
	{
		var vm = CreateViewModel();

		Assert.Equal("Enter", vm.StartKeyLabel);
		Assert.Equal("Enter で開始", vm.Status);
	}

	/// <summary>
	/// パス条件: 開始前に StartKeyLabel を変更すると、案内文がそのラベルを使うように更新される。
	/// </summary>
	[Fact]
	public void ChangingStartKeyLabelBeforeStartUpdatesStatus()
	{
		var vm = CreateViewModel();

		vm.StartKeyLabel = "Return";

		Assert.Equal("Return で開始", vm.Status);
	}

	/// <summary>
	/// パス条件: ゲームオーバー中に StartKeyLabel を変更すると、再開案内文がそのラベルを使うように更新される。
	/// </summary>
	[Fact]
	public void ChangingStartKeyLabelAfterGameOverUpdatesStatus()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);
		// スポーン位置の列を塞いでゲームオーバーにする。
		for (int y = 0; y < 3; y++)
		{
			for (int x = 3; x <= 6; x++)
			{
				vm.Engine.Grid[y, x] = TetrominoType.J;
			}
		}
		vm.Engine.SetCurrentForTest(new Tetromino(TetrominoType.O) { X = 0, Y = 10 });
		vm.Engine.LockCurrentForTest();
		vm.RefreshForTest();
		Assert.True(vm.Engine.IsGameOver);

		vm.StartKeyLabel = "X";

		Assert.Equal("GAME OVER\nX で再開", vm.Status);
	}

	/// <summary>
	/// パス条件: プレイ中に StartKeyLabel を変更しても、プレイ中の Status（空文字）は変化しない
	/// （開始前・ゲームオーバー時以外は案内文を表示しないため）。
	/// </summary>
	[Fact]
	public void ChangingStartKeyLabelWhilePlayingDoesNotChangeStatus()
	{
		var vm = CreateViewModel();
		vm.StartCommand.Execute(null);
		Assert.Equal(string.Empty, vm.Status);

		vm.StartKeyLabel = "X";

		Assert.Equal(string.Empty, vm.Status);
	}
}
