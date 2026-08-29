using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Tetris.ViewModels;

/// <summary>
/// <see cref="INotifyPropertyChanged"/> を実装する ViewModel の基底クラス。
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;

	protected void OnPropertyChanged([CallerMemberName] string? name = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

	/// <summary>値が変化した場合のみフィールドを更新し、変更通知を発行する。</summary>
	protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
		{
			return false;
		}
		field = value;
		OnPropertyChanged(name);
		return true;
	}
}
