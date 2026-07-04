using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
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

    /// <summary>ゲームオーバー時に盤面を下から埋めていく「埋没」演出の 1 行あたりの間隔。</summary>
    private static readonly TimeSpan FillStep = TimeSpan.FromMilliseconds(35);

    // 半透明にして、埋没しても下の積み上がったブロックが透けて見えるようにする。
    private static readonly Color BuryColor = Color.FromArgb(0x80, 0x2A, 0x2A, 0x2E);

    /// <summary>ライン消去アニメーションの総再生時間。</summary>
    private static readonly TimeSpan LineClearDuration = TimeSpan.FromMilliseconds(480);

    private readonly GameViewModel _viewModel = new();
    private readonly DispatcherTimer _buryTimer = new() { Interval = FillStep };
    private readonly DispatcherTimer _lineClearTimer = new() { Interval = LineClearDuration };
    private readonly Random _random = new();
    private readonly List<UIElement> _effectElements = new();
    private int _buryRow;
    private Storyboard? _gameOverInStoryboard;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.StateChanged += (_, _) => Render();
        _viewModel.GameOver += OnGameOver;
        _viewModel.GameStarted += OnGameStarted;
        _viewModel.LinesClearing += OnLinesClearing;
        _viewModel.NewRecord += OnNewRecord;
        _buryTimer.Tick += OnBuryTick;
        _lineClearTimer.Tick += OnLineClearFinished;
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += OnPreviewKeyUp;
        Render();
    }

    /// <summary>
    /// 左右移動キーは DAS/ARR による自前リピートで制御するため、OS のキーリピート（IsRepeat）は無視する。
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.IsRepeat)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Left:
                _viewModel.MoveLeftKeyDown();
                break;
            case Key.Right:
                _viewModel.MoveRightKeyDown();
                break;
        }
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
                _viewModel.MoveLeftKeyUp();
                break;
            case Key.Right:
                _viewModel.MoveRightKeyUp();
                break;
        }
    }

    private void OnLinesClearing(object? sender, LinesClearingEventArgs e)
    {
        bool isTetris = e.Rows.Count >= 4;
        // テトリス（4 ライン）はバナー演出を見せるため長めに再生する。
        _lineClearTimer.Interval = isTetris ? TimeSpan.FromMilliseconds(820) : LineClearDuration;
        PlayLineClearAnimation(e.Rows, isTetris);
        _lineClearTimer.Start();
    }

    private void OnLineClearFinished(object? sender, EventArgs e)
    {
        _lineClearTimer.Stop();
        ClearEffectElements();
        // 実際の消去・下詰めを確定（この後 StateChanged → Render で盤面が描き直される）。
        _viewModel.CompleteLineClear();
    }

    /// <summary>消去対象の行を派手に演出する：白い強発光のストロボ＋縦の弾け、色片の飛散。</summary>
    private void PlayLineClearAnimation(IReadOnlyList<int> rows, bool isTetris)
    {
        var engine = _viewModel.Engine;
        double boardWidth = GameEngine.Columns * CellSize;

        // テトリス時はグローを金色に、点滅・弾けを強くする。
        Color glow = isTetris ? Color.FromRgb(0xFF, 0xD7, 0x4A) : Color.FromRgb(0x9D, 0xF0, 0xFF);
        double blur = isTetris ? 60 : 32;
        double popScale = isTetris ? 3.6 : 2.4;
        int particleCount = isTetris ? 7 : 3;
        double particleSpeed = isTetris ? 230 : 130;
        double particleSizeBonus = isTetris ? 6 : 0;

        foreach (int row in rows)
        {
            // (1) 行全体を覆う閃光バー（ストロボ点滅＋縦に弾ける）
            var flash = new Rectangle
            {
                Width = boardWidth,
                Height = CellSize,
                Fill = new SolidColorBrush(Colors.White),
                Opacity = 0,
                RenderTransformOrigin = new Point(0.5, 0.5),
                Effect = new DropShadowEffect
                {
                    Color = glow,
                    BlurRadius = blur,
                    ShadowDepth = 0,
                    Opacity = 1,
                },
            };
            var flashScale = new ScaleTransform(1, 1);
            flash.RenderTransform = flashScale;
            Canvas.SetLeft(flash, 0);
            Canvas.SetTop(flash, row * CellSize);
            AddEffectElement(flash);

            var strobe = new DoubleAnimationUsingKeyFrames();
            strobe.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
            strobe.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(50))));
            strobe.KeyFrames.Add(new LinearDoubleKeyFrame(0.3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))));
            strobe.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(230))));
            strobe.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(460))));
            flash.BeginAnimation(OpacityProperty, strobe);

            var pop = new DoubleAnimation(1, popScale, TimeSpan.FromMilliseconds(460))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            flashScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);

            // (2) 各ブロックの色片が四方へ飛び散る
            for (int x = 0; x < GameEngine.Columns; x++)
            {
                var type = engine.Grid[row, x];
                if (type is null)
                {
                    continue;
                }
                SpawnParticles(x, row, Tetromino.Colors[type.Value], particleCount, particleSpeed, particleSizeBonus);
            }
        }

        // テトリス専用の追加演出：画面シェイク＋「TETRIS!」バナー。
        if (isTetris)
        {
            ((Storyboard)FindResource("ShakeStoryboard")).Begin(this);
            ShowTetrisBanner();
        }
    }

    /// <summary>4 ライン消し時に中央へ「TETRIS!」を弾けるように表示する。</summary>
    private void ShowTetrisBanner()
    {
        var banner = new TextBlock
        {
            Text = "TETRIS!",
            FontSize = 52,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x4A)),
            Opacity = 0,
            RenderTransformOrigin = new Point(0.5, 0.5),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0xFF, 0x6A, 0x00),
                BlurRadius = 28,
                ShadowDepth = 0,
                Opacity = 1,
            },
        };
        banner.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double left = (GameEngine.Columns * CellSize - banner.DesiredSize.Width) / 2;
        double top = (GameEngine.Rows * CellSize - banner.DesiredSize.Height) / 2;
        Canvas.SetLeft(banner, left);
        Canvas.SetTop(banner, top);

        var scale = new ScaleTransform(0.2, 0.2);
        var rotate = new RotateTransform(-12);
        var group = new TransformGroup();
        group.Children.Add(scale);
        group.Children.Add(rotate);
        banner.RenderTransform = group;
        AddEffectElement(banner);

        var appear = new DoubleAnimation(0.2, 1.0, TimeSpan.FromMilliseconds(420))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.9 },
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, appear);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, appear);
        rotate.BeginAnimation(RotateTransform.AngleProperty,
            new DoubleAnimation(-12, 0, TimeSpan.FromMilliseconds(420)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut } });

        var fade = new DoubleAnimationUsingKeyFrames();
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(620))));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(800))));
        banner.BeginAnimation(OpacityProperty, fade);
    }

    private void SpawnParticles(int col, int row, Color color, int count, double speedBase, double sizeBonus)
    {
        double centerX = col * CellSize + CellSize / 2.0;
        double centerY = row * CellSize + CellSize / 2.0;

        for (int i = 0; i < count; i++)
        {
            double size = 6 + sizeBonus + _random.NextDouble() * 8;
            var particle = new Rectangle
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(color),
                RenderTransformOrigin = new Point(0.5, 0.5),
            };

            var translate = new TranslateTransform();
            var rotate = new RotateTransform();
            var group = new TransformGroup();
            group.Children.Add(rotate);
            group.Children.Add(translate);
            particle.RenderTransform = group;

            Canvas.SetLeft(particle, centerX - size / 2);
            Canvas.SetTop(particle, centerY - size / 2);
            AddEffectElement(particle);

            double angle = _random.NextDouble() * Math.PI * 2;
            double speed = 50 + _random.NextDouble() * speedBase;
            double dx = Math.Cos(angle) * speed;
            double dy = Math.Sin(angle) * speed - 90; // やや上向きに飛ばす
            var duration = TimeSpan.FromMilliseconds(420 + _random.Next(0, 80));

            translate.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(0, dx, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            translate.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(0, dy, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } });
            rotate.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(0, _random.Next(-360, 360), duration));
            particle.BeginAnimation(OpacityProperty,
                new DoubleAnimation(1, 0, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } });
        }
    }

    private void AddEffectElement(UIElement element)
    {
        _effectElements.Add(element);
        GameCanvas.Children.Add(element);
    }

    private void ClearEffectElements()
    {
        foreach (var element in _effectElements)
        {
            GameCanvas.Children.Remove(element);
        }
        _effectElements.Clear();
    }

    private void OnNewRecord(object? sender, EventArgs e) => ShowNewRecordBanner();

    /// <summary>ハイスコア更新時に「NEW RECORD!」を盤面中央へ表示する。</summary>
    private void ShowNewRecordBanner()
    {
        var banner = new TextBlock
        {
            Text = "NEW RECORD!",
            FontSize = 36,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xC2, 0xE7)),
            Opacity = 0,
            RenderTransformOrigin = new Point(0.5, 0.5),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0xCB, 0xA6, 0xF7),
                BlurRadius = 24,
                ShadowDepth = 0,
                Opacity = 1,
            },
        };
        banner.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double left = (GameEngine.Columns * CellSize - banner.DesiredSize.Width) / 2;
        double top = (GameEngine.Rows * CellSize) / 2 - 60;
        Canvas.SetLeft(banner, left);
        Canvas.SetTop(banner, top);

        var scale = new ScaleTransform(0.4, 0.4);
        banner.RenderTransform = scale;
        AddEffectElement(banner);

        var appear = new DoubleAnimation(0.4, 1.0, TimeSpan.FromMilliseconds(350))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 },
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, appear);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, appear);

        var fade = new DoubleAnimationUsingKeyFrames();
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1200))));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1600))));
        banner.BeginAnimation(OpacityProperty, fade);
    }

    private void OnGameOver(object? sender, EventArgs e)
    {
        // 一番下の行から上に向かって灰色ブロックで埋めていく。
        _buryRow = GameEngine.Rows - 1;
        _buryTimer.Start();
    }

    private void OnBuryTick(object? sender, EventArgs e)
    {
        if (_buryRow < 0)
        {
            _buryTimer.Stop();
            ShowGameOverOverlay();
            return;
        }

        for (int x = 0; x < GameEngine.Columns; x++)
        {
            DrawCell(GameCanvas, x, _buryRow, BuryColor);
        }
        _buryRow--;
    }

    private void ShowGameOverOverlay()
    {
        GameOverOverlay.Visibility = Visibility.Visible;

        ((Storyboard)FindResource("ShakeStoryboard")).Begin(this);

        _gameOverInStoryboard = (Storyboard)FindResource("GameOverInStoryboard");
        _gameOverInStoryboard.Begin(this, isControllable: true);
    }

    private void OnGameStarted(object? sender, EventArgs e)
    {
        // 進行中の演出をすべて止めて初期状態に戻す。
        _buryTimer.Stop();
        _lineClearTimer.Stop();
        ClearEffectElements();
        _gameOverInStoryboard?.Stop(this);
        _gameOverInStoryboard = null;

        GameOverOverlay.Visibility = Visibility.Collapsed;
        GameOverOverlay.Opacity = 0;
        GameOverText.Opacity = 1;
        ShakeTransform.X = 0;
        ShakeTransform.Y = 0;
    }

    private void Render()
    {
        DrawBoard();
        DrawNext();
        DrawHold();
    }

    private void DrawBoard()
    {
        var engine = _viewModel.Engine;
        GameCanvas.Children.Clear();

        // 空セルのグリッド線
        var gridStroke = new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
        for (int y = 0; y < GameEngine.Rows; y++)
        {
            for (int x = 0; x < GameEngine.Columns; x++)
            {
                var cell = new Rectangle
                {
                    Width = CellSize,
                    Height = CellSize,
                    Stroke = gridStroke,
                    StrokeThickness = 1,
                };
                Canvas.SetLeft(cell, x * CellSize);
                Canvas.SetTop(cell, y * CellSize);
                GameCanvas.Children.Add(cell);
            }
        }

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

    private void DrawNext() => DrawPiecePreview(NextCanvas, _viewModel.Engine.NextType);

    private void DrawHold() => DrawPiecePreview(HoldCanvas, _viewModel.Engine.HeldType);

    /// <summary>プレビュー枠の中央に指定ピースを描画する（null のときは空にする）。</summary>
    private static void DrawPiecePreview(Canvas canvas, TetrominoType? type)
    {
        canvas.Children.Clear();
        if (type is null)
        {
            return;
        }

        var piece = new Tetromino(type.Value);
        var color = Tetromino.Colors[type.Value];

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
        double left = (canvas.Width - width) / 2;
        double top = (canvas.Height - height) / 2;

        foreach (var (x, y) in piece.Blocks())
        {
            DrawRect(canvas,
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
