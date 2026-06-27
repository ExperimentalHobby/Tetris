using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Tetris;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const int CellSize = 30;

    private readonly GameEngine _engine = new();
    private readonly DispatcherTimer _timer = new();
    private bool _isPaused;
    private bool _isStarted;

    public MainWindow()
    {
        InitializeComponent();
        _timer.Tick += OnTick;
        Render();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_isPaused || _engine.IsGameOver)
        {
            return;
        }
        _engine.SoftDrop();
        _timer.Interval = _engine.DropInterval;
        Render();
        if (_engine.IsGameOver)
        {
            EndGame();
        }
    }

    private void StartGame()
    {
        _engine.Start();
        _isStarted = true;
        _isPaused = false;
        _timer.Interval = _engine.DropInterval;
        _timer.Start();
        StatusText.Text = string.Empty;
        Render();
    }

    private void EndGame()
    {
        _timer.Stop();
        StatusText.Text = "GAME OVER\nEnter で再開";
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            StartGame();
            e.Handled = true;
            return;
        }

        if (!_isStarted || _engine.IsGameOver)
        {
            return;
        }

        if (e.Key == Key.P)
        {
            TogglePause();
            e.Handled = true;
            return;
        }

        if (_isPaused)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Left:
                _engine.MoveLeft();
                break;
            case Key.Right:
                _engine.MoveRight();
                break;
            case Key.Up:
                _engine.Rotate();
                break;
            case Key.Down:
                _engine.SoftDrop();
                _timer.Interval = _engine.DropInterval;
                break;
            case Key.Space:
                _engine.HardDrop();
                _timer.Interval = _engine.DropInterval;
                break;
            default:
                return;
        }

        Render();
        if (_engine.IsGameOver)
        {
            EndGame();
        }
        e.Handled = true;
    }

    private void TogglePause()
    {
        _isPaused = !_isPaused;
        if (_isPaused)
        {
            _timer.Stop();
            StatusText.Text = "PAUSED";
        }
        else
        {
            _timer.Start();
            StatusText.Text = string.Empty;
        }
    }

    private void Render()
    {
        ScoreText.Text = _engine.Score.ToString();
        LinesText.Text = _engine.Lines.ToString();
        LevelText.Text = _engine.Level.ToString();

        DrawBoard();
        DrawNext();
    }

    private void DrawBoard()
    {
        GameCanvas.Children.Clear();

        // 固定済みブロック
        for (int y = 0; y < GameEngine.Rows; y++)
        {
            for (int x = 0; x < GameEngine.Columns; x++)
            {
                var type = _engine.Grid[y, x];
                if (type is not null)
                {
                    DrawCell(GameCanvas, x, y, Tetromino.Colors[type.Value]);
                }
            }
        }

        // ゴースト（着地予測）
        if (_engine.Current is { } current)
        {
            int ghostY = _engine.GhostY();
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

            // 落下中のピース
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
        NextCanvas.Children.Clear();
        var piece = new Tetromino(_engine.NextType);
        var color = Tetromino.Colors[_engine.NextType];

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
