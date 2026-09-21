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

	/// <summary>KeyEventArgs.IsRepeat の内部バッキングフィールド。OS のキーボード状態から自動算出されるため、
	/// テストから IsRepeat=true の入力を作るにはリフレクションで直接書き換える必要がある。</summary>
	private static readonly System.Reflection.FieldInfo IsRepeatField =
		typeof(KeyEventArgs).GetField("_isRepeat", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

	/// <summary>
	/// 指定した要素に対して PreviewKeyDown を実際の RoutedEvent 経由で発火させる。
	/// キーコンフィグのキー変更待機（KeyConfigWindow.OnDialogPreviewKeyDown）や
	/// MainWindow.OnPreviewKeyDown を、実キーボード入力を使わずに検証するために使う。
	/// </summary>
	/// <param name="isRepeat">
	/// OS のキーリピートによる入力を模擬するかどうか。KeyEventArgs.IsRepeat は実際のキーボード状態から
	/// 算出されるためコンストラクタ引数では指定できず、内部バッキングフィールドを直接書き換える。
	/// </param>
	public static void SimulateKeyDown(UIElement target, Key key, bool isRepeat = false)
	{
		var args = new KeyEventArgs(Keyboard.PrimaryDevice, new FakePresentationSource(), 0, key)
		{
			RoutedEvent = UIElement.PreviewKeyDownEvent,
		};
		IsRepeatField.SetValue(args, isRepeat);
		target.RaiseEvent(args);
	}

	/// <summary>指定した要素に対して PreviewKeyUp を実際の RoutedEvent 経由で発火させる。</summary>
	public static void SimulateKeyUp(UIElement target, Key key)
	{
		var args = new KeyEventArgs(Keyboard.PrimaryDevice, new FakePresentationSource(), 0, key)
		{
			RoutedEvent = UIElement.PreviewKeyUpEvent,
		};
		target.RaiseEvent(args);
	}
}
