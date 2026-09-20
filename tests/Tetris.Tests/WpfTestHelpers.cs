using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Tetris.Tests;

/// <summary>
/// 設定ダイアログ（KeyConfigWindow / AutoRepeatConfigWindow）等の WPF コードビハインドを、
/// 実際のウィンドウを生成した上でユーザー操作に近い形でテストするための共通ヘルパー。
/// </summary>
internal static class WpfTestHelpers
{
	/// <summary>
	/// 実際の入力デバイスを介さずに Window.ShowDialog() を成立させるためのダミー PresentationSource。
	/// KeyEventArgs のコンストラクタが inputSource の null を許さないため、テスト用にダミーを渡す。
	/// </summary>
	private sealed class FakePresentationSource : PresentationSource
	{
		private Visual? _rootVisual;

		public override Visual RootVisual
		{
			get => _rootVisual!;
			set => _rootVisual = value;
		}

		public override bool IsDisposed => false;

		protected override CompositionTarget? GetCompositionTargetCore() => null;
	}

	/// <summary>実際にクリックされたのと同じ RoutedEvent(Button.Click)を発火させる。</summary>
	public static void Click(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

	/// <summary>
	/// 論理ツリーを再帰的にたどり、指定した Content を持つ Button を探す。
	/// 動的に生成され x:Name を持たないボタン（KeyConfigWindow の行内「変更」ボタン等）を
	/// テストから参照するために使う。
	/// </summary>
	/// <exception cref="InvalidOperationException">一致する Button が見つからない場合。</exception>
	public static Button FindButtonByContent(DependencyObject root, object content)
	{
		foreach (var child in LogicalTreeHelper.GetChildren(root))
		{
			if (child is not DependencyObject node)
			{
				continue;
			}
			if (node is Button button && Equals(button.Content, content))
			{
				return button;
			}
			try
			{
				return FindButtonByContent(node, content);
			}
			catch (InvalidOperationException)
			{
				// このノード配下には無かった。兄弟ノードを引き続き探索する。
			}
		}
		throw new InvalidOperationException($"Content '{content}' の Button が見つかりません。");
	}

	/// <summary>
	/// 指定した要素に対して PreviewKeyDown を実際の RoutedEvent 経由で発火させる。
	/// キーコンフィグのキー変更待機（KeyConfigWindow.OnDialogPreviewKeyDown）を、
	/// 実キーボード入力を使わずに検証するために使う。
	/// </summary>
	public static void SimulateKeyDown(UIElement target, Key key)
	{
		var args = new KeyEventArgs(Keyboard.PrimaryDevice, new FakePresentationSource(), 0, key)
		{
			RoutedEvent = UIElement.PreviewKeyDownEvent,
		};
		target.RaiseEvent(args);
	}
}
