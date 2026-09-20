using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using Tetris.Input;
using Tetris.Services;

namespace Tetris.Tests;

/// <summary>
/// <see cref="KeyConfigWindow"/> の実際のウィンドウ（行構築・キー変更待機・保存/キャンセル）を検証するテスト。
/// 副作用（実ファイル書き込み）を避けるため、<see cref="KeyBindingService"/> は一時ディレクトリに向けて生成する。
/// </summary>
public class KeyConfigWindowTests : IDisposable
{
	private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

	private KeyBindingService CreateService() => new(_tempDir);

	public void Dispose()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// 指定した操作の行（keyText の TextBlock と 変更 Button の組）を、
	/// コンストラクタ直後の表示内容（referenceBindings が示すキー）から特定する。
	/// </summary>
	private static (TextBlock KeyText, Button ChangeButton) FindRow(KeyConfigWindow window, KeyBindings referenceBindings, GameAction action)
	{
		string expectedText = KeyDisplay.ToDisplayString(referenceBindings.GetKey(action));
		foreach (var child in window.RowsPanel.Children.OfType<Grid>())
		{
			var texts = child.Children.OfType<TextBlock>().ToList();
			if (texts.Count == 2 && texts[1].Text == expectedText)
			{
				return (texts[1], child.Children.OfType<Button>().Single());
			}
		}
		throw new InvalidOperationException($"{action} の行が見つかりません。");
	}

	/// <summary>パス条件: コンストラクタ実行後、全10操作分の行が RowsPanel に構築される。</summary>
	[Fact]
	public void ConstructorBuildsRowForEveryAction()
	{
		StaTestRunner.Run(() =>
		{
			var window = new KeyConfigWindow(KeyBindings.Default(), CreateService());

			int actionCount = Enum.GetValues<GameAction>().Length;
			Assert.Equal(actionCount, window.RowsPanel.Children.OfType<Grid>().Count());
		});
	}

	/// <summary>パス条件: 各行のキー表示は、渡した現在のキーコンフィグの内容と一致する。</summary>
	[Fact]
	public void ConstructorDisplaysCurrentKeyForEachAction()
	{
		StaTestRunner.Run(() =>
		{
			var current = KeyBindings.Default();
			current.TrySetKey(GameAction.Rotate, Key.X);
			var window = new KeyConfigWindow(current, CreateService());

			var (keyText, _) = FindRow(window, current, GameAction.Rotate);

			Assert.Equal("X", keyText.Text);
		});
	}

	/// <summary>
	/// パス条件: コンストラクタに渡した KeyBindings インスタンス（呼び出し元の current）は、
	/// ウィンドウ内の操作によって変更されない（独立した作業用コピーを持つ）。
	/// </summary>
	[Fact]
	public void ConstructorDoesNotMutateCallerBindings()
	{
		StaTestRunner.Run(() =>
		{
			var current = KeyBindings.Default();
			var window = new KeyConfigWindow(current, CreateService());
			var (_, changeButton) = FindRow(window, current, GameAction.Rotate);

			WpfTestHelpers.Click(changeButton);
			WpfTestHelpers.SimulateKeyDown(window, Key.X);

			Assert.Equal(Key.Up, current.GetKey(GameAction.Rotate));
		});
	}

	/// <summary>
	/// パス条件: 「変更」をクリックして待機状態にし、キーを押すとその行の表示が新しいキーに変わる。
	/// </summary>
	[Fact]
	public void ChangeButtonClickThenKeyPressUpdatesDisplayedKey()
	{
		StaTestRunner.Run(() =>
		{
			var current = KeyBindings.Default();
			var window = new KeyConfigWindow(current, CreateService());
			var (keyText, changeButton) = FindRow(window, current, GameAction.Rotate);

			WpfTestHelpers.Click(changeButton);
			Assert.Equal("キーを押してください...", keyText.Text);

			WpfTestHelpers.SimulateKeyDown(window, Key.X);

			Assert.Equal("X", keyText.Text);
		});
	}

	/// <summary>
	/// パス条件: 「変更」を押さず待機状態でない間にキーを押しても、何も変化しない
	/// （OnDialogPreviewKeyDown の _listeningAction が null のときの早期リターン経路）。
	/// </summary>
	[Fact]
	public void KeyPressWithoutListeningDoesNothing()
	{
		StaTestRunner.Run(() =>
		{
			var current = KeyBindings.Default();
			var window = new KeyConfigWindow(current, CreateService());
			var (keyText, _) = FindRow(window, current, GameAction.Rotate);

			WpfTestHelpers.SimulateKeyDown(window, Key.X);

			Assert.Equal("↑", keyText.Text);
		});
	}

	/// <summary>パス条件: キー変更待機中に Escape を押すと、変更前のキーのまま待機が解除される。</summary>
	[Fact]
	public void EscapeKeyDuringListeningCancelsChange()
	{
		StaTestRunner.Run(() =>
		{
			var current = KeyBindings.Default();
			var window = new KeyConfigWindow(current, CreateService());
			var (keyText, changeButton) = FindRow(window, current, GameAction.Rotate);

			WpfTestHelpers.Click(changeButton);
			WpfTestHelpers.SimulateKeyDown(window, Key.Escape);

			Assert.Equal("↑", keyText.Text);
		});
	}

	/// <summary>パス条件: 「既定に戻す」をクリックすると、変更した内容が既定のキーコンフィグに戻る。</summary>
	[Fact]
	public void ResetButtonClickRestoresDefaultBindings()
	{
		StaTestRunner.Run(() =>
		{
			var current = KeyBindings.Default();
			var window = new KeyConfigWindow(current, CreateService());
			var (keyText, changeButton) = FindRow(window, current, GameAction.Rotate);
			WpfTestHelpers.Click(changeButton);
			WpfTestHelpers.SimulateKeyDown(window, Key.X);
			Assert.Equal("X", keyText.Text);

			WpfTestHelpers.Click(WpfTestHelpers.FindButtonByContent(window, "既定に戻す"));

			Assert.Equal("↑", keyText.Text);
		});
	}

	/// <summary>
	/// パス条件: 「保存」をクリックすると、変更後のキーコンフィグが Service 経由で永続化され、
	/// Result に反映され、DialogResult が true になる。
	/// </summary>
	[Fact]
	public void SaveButtonClickPersistsViaServiceAndSetsResult()
	{
		StaTestRunner.Run(() =>
		{
			var current = KeyBindings.Default();
			var service = CreateService();
			var window = new KeyConfigWindow(current, service);
			var (_, changeButton) = FindRow(window, current, GameAction.Rotate);
			WpfTestHelpers.Click(changeButton);
			WpfTestHelpers.SimulateKeyDown(window, Key.X);
			window.Dispatcher.BeginInvoke(new Action(() =>
				WpfTestHelpers.Click(WpfTestHelpers.FindButtonByContent(window, "保存"))));

			bool? dialogResult = window.ShowDialog();

			Assert.True(dialogResult);
			Assert.Equal(Key.X, window.Result!.GetKey(GameAction.Rotate));
			Assert.Equal(Key.X, service.Load().GetKey(GameAction.Rotate));
		});
	}

	/// <summary>
	/// パス条件: 「キャンセル」をクリックすると、Service への保存は行われず Result は null のまま
	/// DialogResult が false になる。
	/// </summary>
	[Fact]
	public void CancelButtonClickDoesNotPersistAndClosesWithNullResult()
	{
		StaTestRunner.Run(() =>
		{
			var current = KeyBindings.Default();
			var service = CreateService();
			var window = new KeyConfigWindow(current, service);
			var (_, changeButton) = FindRow(window, current, GameAction.Rotate);
			WpfTestHelpers.Click(changeButton);
			WpfTestHelpers.SimulateKeyDown(window, Key.X);
			window.Dispatcher.BeginInvoke(new Action(() =>
				WpfTestHelpers.Click(WpfTestHelpers.FindButtonByContent(window, "キャンセル"))));

			bool? dialogResult = window.ShowDialog();

			Assert.False(dialogResult);
			Assert.Null(window.Result);
			Assert.Equal(Key.Up, service.Load().GetKey(GameAction.Rotate));
		});
	}
}
