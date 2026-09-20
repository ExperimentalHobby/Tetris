using System.Windows.Input;
using Tetris.ViewModels;

namespace Tetris.Tests;

/// <summary>
/// <see cref="RelayCommand"/> の実行・実行可否判定・変更通知を検証するテスト。
/// </summary>
public class RelayCommandTests
{
	/// <summary>パス条件: canExecute を省略した場合、CanExecute は常に true。</summary>
	[Fact]
	public void CanExecuteWithoutPredicateAlwaysReturnsTrue()
	{
		var command = new RelayCommand(() => { });

		Assert.True(command.CanExecute(null));
	}

	/// <summary>パス条件: canExecute が false を返す間、CanExecute も false。</summary>
	[Fact]
	public void CanExecuteDelegatesToPredicate()
	{
		var command = new RelayCommand(() => { }, () => false);

		Assert.False(command.CanExecute(null));
	}

	/// <summary>パス条件: Execute() を呼ぶと渡した Action が実行される。</summary>
	[Fact]
	public void ExecuteInvokesAction()
	{
		bool invoked = false;
		var command = new RelayCommand(() => invoked = true);

		command.Execute(null);

		Assert.True(invoked);
	}

	/// <summary>
	/// パス条件: CanExecuteChanged に登録したハンドラは、
	/// CommandManager.RequerySuggested（CommandManager.InvalidateRequerySuggested() 経由）の発火で呼ばれる。
	/// CommandManager の再評価は Dispatcher の BeginInvoke 経由で非同期に行われるため、
	/// 実際にメッセージポンプを回す StaTestRunner 上で検証する。
	/// </summary>
	[Fact]
	public void CanExecuteChangedIsRaisedThroughCommandManager()
	{
		var command = new RelayCommand(() => { });
		int raisedCount = 0;
		EventHandler handler = (_, _) => raisedCount++;

		StaTestRunner.RunWithMessagePump(
			setup: _ =>
			{
				command.CanExecuteChanged += handler;
				CommandManager.InvalidateRequerySuggested();
			},
			until: () => raisedCount > 0,
			timeout: TimeSpan.FromSeconds(3));

		command.CanExecuteChanged -= handler;
		Assert.True(raisedCount > 0);
	}

	/// <summary>
	/// パス条件: CanExecuteChanged から登録解除したハンドラは、
	/// その後 CommandManager.InvalidateRequerySuggested() を呼んでも発火しない。
	/// </summary>
	[Fact]
	public void CanExecuteChangedAfterUnsubscribeIsNotRaised()
	{
		var command = new RelayCommand(() => { });
		int raisedCount = 0;
		EventHandler handler = (_, _) => raisedCount++;

		StaTestRunner.RunWithMessagePump(
			setup: _ =>
			{
				command.CanExecuteChanged += handler;
				command.CanExecuteChanged -= handler;
				CommandManager.InvalidateRequerySuggested();
			},
			until: () => false,
			timeout: TimeSpan.FromMilliseconds(300));

		Assert.Equal(0, raisedCount);
	}
}
