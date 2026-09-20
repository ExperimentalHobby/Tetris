using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace Tetris.Tests;

/// <summary>
/// WPF の Window / DispatcherTimer 等、STAスレッド上でしか正しく動かないコードをテストするためのヘルパー。
/// バックグラウンドの STA スレッドで処理を実行し、完了を待ち、処理中の例外は呼び出し元スレッドへ再スローする。
/// </summary>
internal static class StaTestRunner
{
	/// <summary>指定した処理を STA スレッド上で実行し、完了を待つ。処理中の例外は呼び出し元へ再スローする。</summary>
	public static void Run(Action action)
	{
		Exception? captured = null;
		var thread = new Thread(() =>
		{
			try
			{
				action();
			}
			catch (Exception ex)
			{
				captured = ex;
			}
		});
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		thread.Join();
		if (captured is not null)
		{
			ExceptionDispatchInfo.Capture(captured).Throw();
		}
	}

	/// <summary>
	/// STA スレッド上で Dispatcher を起動し、<paramref name="setup"/> を実行した後、
	/// <paramref name="until"/> が true を返すか <paramref name="timeout"/> に達するまで
	/// 実際にメッセージ（DispatcherTimer の Tick 等）を処理し続ける。
	/// テスト対象が DispatcherTimer に登録したハンドラを、フェイクではなく実際の
	/// メッセージポンプ経由で発火させて検証するために使う。
	/// </summary>
	public static void RunWithMessagePump(Action<Dispatcher> setup, Func<bool> until, TimeSpan timeout)
	{
		Exception? captured = null;
		var thread = new Thread(() =>
		{
			try
			{
				var dispatcher = Dispatcher.CurrentDispatcher;
				setup(dispatcher);

				var frame = new DispatcherFrame();
				var deadline = DateTime.UtcNow + timeout;
				var poller = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5) };
				poller.Tick += (_, _) =>
				{
					if (until() || DateTime.UtcNow > deadline)
					{
						poller.Stop();
						frame.Continue = false;
					}
				};
				poller.Start();
				Dispatcher.PushFrame(frame);
			}
			catch (Exception ex)
			{
				captured = ex;
			}
		});
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		thread.Join();
		if (captured is not null)
		{
			ExceptionDispatchInfo.Capture(captured).Throw();
		}
	}
}
