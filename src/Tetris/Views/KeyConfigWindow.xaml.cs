using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Tetris.Input;
using Tetris.Services;

namespace Tetris;

/// <summary>
/// キーコンフィグ（キーリマップ）用のモーダルダイアログ。
/// 「変更」を押すと次に押されたキーで該当操作を上書きする（他操作と重複する場合は入れ替え）。
/// 「保存」を押すまでは <see cref="KeyBindingService"/> への永続化は行わない。
/// </summary>
public partial class KeyConfigWindow : Window
{
    private static readonly (GameAction Action, string Label)[] Rows =
    {
        (GameAction.MoveLeft, "左移動"),
        (GameAction.MoveRight, "右移動"),
        (GameAction.Rotate, "回転"),
        (GameAction.RotateCcw, "逆回転"),
        (GameAction.SoftDrop, "ソフトドロップ"),
        (GameAction.HardDrop, "ハードドロップ"),
        (GameAction.Hold, "ホールド"),
        (GameAction.Start, "開始 / リスタート"),
        (GameAction.Pause, "一時停止"),
        (GameAction.ToggleMute, "ミュート切替"),
    };

    private readonly KeyBindingService _service;
    private readonly Dictionary<GameAction, TextBlock> _keyTexts = new();
    private KeyBindings _workingBindings;
    private GameAction? _listeningAction;

    /// <summary>「保存」で閉じられた場合に確定したキー割り当て。それ以外（キャンセル等）は null。</summary>
    public KeyBindings? Result { get; private set; }

    public KeyConfigWindow(KeyBindings current, KeyBindingService service)
    {
        InitializeComponent();
        _service = service;
        // 呼び出し元の current を直接変更しないよう独立した作業用コピーを持つ。
        _workingBindings = KeyBindings.FromSaved(current.ToDictionary());
        BuildRows();
        PreviewKeyDown += OnDialogPreviewKeyDown;
    }

    private void BuildRows()
    {
        RowsPanel.Children.Clear();
        _keyTexts.Clear();
        foreach (var (action, label) in Rows)
        {
            var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

            var labelText = new TextBlock
            {
                Text = label,
                Foreground = Brushes.WhiteSmoke,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(labelText, 0);

            var keyText = new TextBlock
            {
                Text = KeyDisplay.ToDisplayString(_workingBindings.GetKey(action)),
                Foreground = (Brush)new BrushConverter().ConvertFromString("#89DCEB")!,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            Grid.SetColumn(keyText, 1);
            _keyTexts[action] = keyText;

            var changeButton = new Button { Content = "変更", Padding = new Thickness(8, 2, 8, 2) };
            changeButton.Click += (_, _) => BeginListening(action);
            Grid.SetColumn(changeButton, 2);

            row.Children.Add(labelText);
            row.Children.Add(keyText);
            row.Children.Add(changeButton);
            RowsPanel.Children.Add(row);
        }
    }

    /// <summary>指定した操作を「次に押されたキーで上書きする」待機状態にする。</summary>
    private void BeginListening(GameAction action)
    {
        _listeningAction = action;
        _keyTexts[action].Text = "キーを押してください...";
    }

    /// <summary>キー変更待機中に押されたキーで該当操作を上書きする。Escape で待機をキャンセルする。</summary>
    private void OnDialogPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_listeningAction is not { } action)
        {
            return;
        }
        e.Handled = true;
        if (e.Key != Key.Escape)
        {
            _workingBindings.TrySetKey(action, e.Key);
        }
        _listeningAction = null;
        RefreshKeyTexts();
    }

    private void RefreshKeyTexts()
    {
        foreach (var (action, _) in Rows)
        {
            _keyTexts[action].Text = KeyDisplay.ToDisplayString(_workingBindings.GetKey(action));
        }
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        _listeningAction = null;
        _workingBindings = KeyBindings.Default();
        RefreshKeyTexts();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        _service.Save(_workingBindings);
        Result = _workingBindings;
        DialogResult = true;
        Close();
    }
}
