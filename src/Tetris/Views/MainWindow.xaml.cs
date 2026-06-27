using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Tetris.ViewModels;

namespace Tetris;

/// <summary>
/// Interaction logic for MainWindow.xaml
///
/// ハイブリッド MVVM: ゲーム進行と状態は <see cref="GameViewModel"/> が担い、
/// このコードビハインドは性能上の理由から Canvas への盤面描画のみを受け持つ。
/// </summary>
public partial class MainWindow : Window
{
    private const int CellSize = 30;

    private readonly GameViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.StateChanged += (_, _) => Render();
        Render();
    }

    private void Render()
    {
        DrawBoard();
        DrawNext();
    }

    private void DrawBoard()
    {
        var engine = _viewModel.Engine;
        GameCanvas.Children.Clear();

        // 固定済みブロック
        for (int y = 0; y < GameEngine.Rows; y++)
        {
            for (int x = 0; x < GameEngine.Columns; x++)
            {
                var type = engine.Grid[y, x];
                if (type is not null)
                {
                    DrawCell(GameCanvas, x, y, Tetromino.Colors[type.Value]);
                }
            }
        }

        // ゴースト（着地予測）と落下中のピース
        if (engine.Current is { } current)
        {
            int ghostY = engine.GhostY();
            int offset = ghostY - current.Y;
            var ghostColor = Tetromino.Colors[current.Type];
            foreach (var (bx, by) in current.Blocks())
            {
                int gy = by + offset;
                if (gy >= 0)
                {
                    DrawCell(GameCanvas, bx, gy,
                        Color.FromArgb(60, ghostColor.R, ghostColor.G, ghostColor.B));
                }
            }

            foreach (var (bx, by) in current.Blocks())
            {
                if (by >= 0)
                {
                    DrawCell(GameCanvas, bx, by, Tetromino.Colors[current.Type]);
                }
            }
        }
    }

    private void DrawNext()
    {
        var engine = _viewModel.Engine;
        NextCanvas.Children.Clear();
        var piece = new Tetromino(engine.NextType);
        var color = Tetromino.Colors[engine.NextType];

        // プレビュー領域中央に配置するためのオフセットを計算
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (var (x, y) in piece.Blocks())
        {
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }

        const int preview = 24;
        double width = (maxX - minX + 1) * preview;
        double height = (maxY - minY + 1) * preview;
        double left = (NextCanvas.Width - width) / 2;
        double top = (NextCanvas.Height - height) / 2;

        foreach (var (x, y) in piece.Blocks())
        {
            DrawRect(NextCanvas,
                left + (x - minX) * preview,
                top + (y - minY) * preview,
                preview, color);
        }
    }

    private static void DrawCell(Canvas canvas, int col, int row, Color color)
    {
        DrawRect(canvas, col * CellSize, row * CellSize, CellSize, color);
    }

    private static void DrawRect(Canvas canvas, double x, double y, double size, Color color)
    {
        var rect = new Rectangle
        {
            Width = size,
            Height = size,
            Fill = new SolidColorBrush(color),
            Stroke = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)),
            StrokeThickness = 1,
        };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        canvas.Children.Add(rect);
    }
}
