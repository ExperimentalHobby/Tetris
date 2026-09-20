using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Tetris.Tests;

/// <summary>
/// <see cref="WpfTestHelpers"/> 自体の挙動を検証するテスト。
/// </summary>
public class WpfTestHelpersTests
{
	/// <summary>パス条件: Click() で Button の Click イベントハンドラが呼ばれる。</summary>
	[Fact]
	public void ClickRaisesButtonClickEvent()
	{
		StaTestRunner.Run(() =>
		{
			bool clicked = false;
			var button = new Button();
			button.Click += (_, _) => clicked = true;

			WpfTestHelpers.Click(button);

			Assert.True(clicked);
		});
	}

	/// <summary>パス条件: FindButtonByContent() が論理ツリー内から Content が一致する Button を見つける。</summary>
	[Fact]
	public void FindButtonByContentFindsNestedButton()
	{
		StaTestRunner.Run(() =>
		{
			var inner = new StackPanel();
			var button = new Button { Content = "保存" };
			inner.Children.Add(button);
			var outer = new StackPanel();
			outer.Children.Add(inner);

			var found = WpfTestHelpers.FindButtonByContent(outer, "保存");

			Assert.Same(button, found);
		});
	}

	/// <summary>パス条件: 一致する Content の Button が無ければ例外を投げる。</summary>
	[Fact]
	public void FindButtonByContentWithNoMatchThrows()
	{
		StaTestRunner.Run(() =>
		{
			var panel = new StackPanel();

			Assert.Throws<InvalidOperationException>(() => WpfTestHelpers.FindButtonByContent(panel, "存在しない"));
		});
	}

	/// <summary>パス条件: SimulateKeyDown() で対象要素の PreviewKeyDown ハンドラが指定したキーで呼ばれる。</summary>
	[Fact]
	public void SimulateKeyDownRaisesPreviewKeyDownWithSpecifiedKey()
	{
		StaTestRunner.Run(() =>
		{
			var window = new Window();
			Key? observed = null;
			window.PreviewKeyDown += (_, e) => observed = e.Key;

			WpfTestHelpers.SimulateKeyDown(window, Key.Escape);

			Assert.Equal(Key.Escape, observed);
		});
	}
}
