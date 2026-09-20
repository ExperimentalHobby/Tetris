using System.ComponentModel;
using Tetris.ViewModels;

namespace Tetris.Tests;

/// <summary>
/// <see cref="ObservableObject"/> の変更通知（<see cref="ObservableObject.SetProperty{T}"/>）を検証するテスト。
/// </summary>
public class ObservableObjectTests
{
	private sealed class TestViewModel : ObservableObject
	{
		private int _value;

		public int Value
		{
			get => _value;
			set => SetProperty(ref _value, value);
		}
	}

	/// <summary>パス条件: 値が変化した場合、SetProperty は true を返し PropertyChanged が発火する。</summary>
	[Fact]
	public void SetPropertyWithDifferentValueRaisesPropertyChanged()
	{
		var vm = new TestViewModel();
		string? raisedPropertyName = null;
		vm.PropertyChanged += (_, e) => raisedPropertyName = e.PropertyName;

		vm.Value = 42;

		Assert.Equal(42, vm.Value);
		Assert.Equal(nameof(TestViewModel.Value), raisedPropertyName);
	}

	/// <summary>パス条件: 同値を設定した場合、PropertyChanged は発火しない。</summary>
	[Fact]
	public void SetPropertyWithSameValueDoesNotRaisePropertyChanged()
	{
		var vm = new TestViewModel { Value = 10 };
		bool raised = false;
		vm.PropertyChanged += (_, _) => raised = true;

		vm.Value = 10;

		Assert.False(raised);
	}

	/// <summary>パス条件: PropertyChanged に購読者がいない状態で値を変更しても例外にならない（null条件演算子の分岐）。</summary>
	[Fact]
	public void SetPropertyWithoutSubscribersDoesNotThrow()
	{
		var vm = new TestViewModel();

		vm.Value = 1;

		Assert.Equal(1, vm.Value);
	}
}
