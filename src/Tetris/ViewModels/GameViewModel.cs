using System.Windows.Threading;
using Tetris.Input;
using Tetris.Services;

namespace Tetris.ViewModels;

/// <summary>
/// ゲーム進行を司る ViewModel。スコア等の状態をバインディングで公開し、入力をコマンド化する。
/// 盤面の描画自体は View（Canvas）が <see cref="Engine"/> を読み取って行う（ハイブリッド MVVM）。
/// </summary>
public sealed class GameViewModel : ObservableObject
{
    private readonly GameEngine _engine = new();
    private readonly DispatcherTimer _timer = new();
    private readonly DispatcherTimer _inputTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly AutoRepeatController _leftRepeat = new();
    private readonly AutoRepeatController _rightRepeat = new();
    private readonly HighScoreService _highScoreService = new();
    private readonly SoundEffectService _soundService = new();

    private bool _isStarted;
    private bool _isPaused;
    private bool _gameOverNotified;

    private int _score;
    private int _lines;
    private int _level = 1;
    private int _highScore;
    private string _status = "Enter で開始";

    public GameViewModel()
    {
        _highScore = _highScoreService.Load();
        _engine.PieceLocked += (_, _) => _soundService.PlayLock();
        _timer.Tick += OnTick;
        _inputTimer.Tick += OnInputTick;

        StartCommand = new RelayCommand(Start);
        MoveLeftCommand = new RelayCommand(MoveLeft, CanPlay);
        MoveRightCommand = new RelayCommand(MoveRight, CanPlay);
        RotateCommand = new RelayCommand(Rotate, CanPlay);
        RotateCcwCommand = new RelayCommand(RotateCcw, CanPlay);
        SoftDropCommand = new RelayCommand(SoftDrop, CanPlay);
        HardDropCommand = new RelayCommand(HardDrop, CanPlay);
        HoldCommand = new RelayCommand(Hold, CanPlay);
        PauseCommand = new RelayCommand(TogglePause, () => _isStarted && !_engine.IsGameOver && !_engine.IsClearing);
        ToggleMuteCommand = new RelayCommand(ToggleMute);
    }

    /// <summary>描画に必要な盤面情報へのアクセス（View が読み取り専用で参照する）。</summary>
    public GameEngine Engine => _engine;

    /// <summary>盤面状態が変化したことを View に通知する（Canvas 再描画用）。</summary>
    public event EventHandler? StateChanged;

    /// <summary>ゲームオーバーになった瞬間に一度だけ発火する（演出開始用）。</summary>
    public event EventHandler? GameOver;

    /// <summary>ゲーム開始（リスタート含む）の瞬間に発火する（演出のリセット用）。</summary>
    public event EventHandler? GameStarted;

    /// <summary>満杯行が揃った瞬間に発火する。View が消去アニメーションを再生する。</summary>
    public event EventHandler<LinesClearingEventArgs>? LinesClearing;

    /// <summary>ハイスコアを更新した瞬間に発火する（NEW RECORD 演出用）。</summary>
    public event EventHandler? NewRecord;

    public int Score { get => _score; private set => SetProperty(ref _score, value); }
    public int Lines { get => _lines; private set => SetProperty(ref _lines, value); }
    public int Level { get => _level; private set => SetProperty(ref _level, value); }
    public int HighScore { get => _highScore; private set => SetProperty(ref _highScore, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    /// <summary>効果音の再生音量（0.0〜1.0）。</summary>
    public double Volume
    {
        get => _soundService.Volume;
        set
        {
            if (_soundService.Volume != value)
            {
                _soundService.Volume = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>ミュート中かどうか。</summary>
    public bool IsMuted
    {
        get => _soundService.IsMuted;
        set
        {
            if (_soundService.IsMuted != value)
            {
                _soundService.IsMuted = value;
                OnPropertyChanged();
            }
        }
    }

    public RelayCommand StartCommand { get; }
    public RelayCommand MoveLeftCommand { get; }
    public RelayCommand MoveRightCommand { get; }
    public RelayCommand RotateCommand { get; }
    public RelayCommand RotateCcwCommand { get; }
    public RelayCommand SoftDropCommand { get; }
    public RelayCommand HardDropCommand { get; }
    public RelayCommand HoldCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand ToggleMuteCommand { get; }

    private bool CanPlay() => _isStarted && !_isPaused && !_engine.IsGameOver && !_engine.IsClearing;

    private void Start()
    {
        _engine.Start();
        _isStarted = true;
        _isPaused = false;
        _gameOverNotified = false;
        _leftRepeat.KeyUp();
        _rightRepeat.KeyUp();
        _timer.Interval = _engine.DropInterval;
        _timer.Start();
        _inputTimer.Start();
        Status = string.Empty;
        GameStarted?.Invoke(this, EventArgs.Empty);
        AfterChange();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_isPaused || _engine.IsGameOver || _engine.IsClearing)
        {
            return;
        }
        var interval = _timer.Interval;
        _engine.SoftDrop();
        _engine.AdvanceLockDelay(interval);
        _timer.Interval = _engine.DropInterval;
        AfterChange();
    }

    /// <summary>左右移動キーが押された/離されたことを通知する（DAS/ARR による自前リピートのため）。</summary>
    public void MoveLeftKeyDown()
    {
        if (!CanPlay())
        {
            return;
        }
        _leftRepeat.KeyDown();
        MoveLeft();
    }

    public void MoveLeftKeyUp() => _leftRepeat.KeyUp();

    public void MoveRightKeyDown()
    {
        if (!CanPlay())
        {
            return;
        }
        _rightRepeat.KeyDown();
        MoveRight();
    }

    public void MoveRightKeyUp() => _rightRepeat.KeyUp();

    /// <summary>DAS/ARR の経過を進め、リピート分の左右移動を行う専用タイマーの Tick。</summary>
    private void OnInputTick(object? sender, EventArgs e)
    {
        if (_isPaused || _engine.IsGameOver || _engine.IsClearing)
        {
            return;
        }
        var interval = _inputTimer.Interval;
        int leftRepeats = _leftRepeat.Advance(interval);
        for (int i = 0; i < leftRepeats; i++)
        {
            _engine.MoveLeft();
        }
        int rightRepeats = _rightRepeat.Advance(interval);
        for (int i = 0; i < rightRepeats; i++)
        {
            _engine.MoveRight();
        }
        if (leftRepeats > 0 || rightRepeats > 0)
        {
            AfterChange();
        }
    }

    /// <summary>消去アニメーション完了後に View から呼ばれ、実際の消去・下詰めを確定する。</summary>
    public void CompleteLineClear()
    {
        if (!_engine.IsClearing)
        {
            return;
        }
        _engine.CommitClear();
        if (!_engine.IsGameOver)
        {
            _timer.Interval = _engine.DropInterval;
            _timer.Start();
            _inputTimer.Start();
        }
        AfterChange();
    }

    private void MoveLeft()
    {
        _engine.MoveLeft();
        AfterChange();
    }

    private void MoveRight()
    {
        _engine.MoveRight();
        AfterChange();
    }

    private void Rotate()
    {
        if (_engine.Rotate())
        {
            _soundService.PlayRotate();
        }
        AfterChange();
    }

    private void RotateCcw()
    {
        if (_engine.RotateCcw())
        {
            _soundService.PlayRotate();
        }
        AfterChange();
    }

    private void SoftDrop()
    {
        _engine.SoftDrop();
        _timer.Interval = _engine.DropInterval;
        AfterChange();
    }

    private void HardDrop()
    {
        _engine.HardDrop();
        _timer.Interval = _engine.DropInterval;
        AfterChange();
    }

    private void Hold()
    {
        _engine.Hold();
        AfterChange();
    }

    private void ToggleMute() => IsMuted = !IsMuted;

    private void TogglePause()
    {
        if (!_isStarted || _engine.IsGameOver)
        {
            return;
        }
        _isPaused = !_isPaused;
        if (_isPaused)
        {
            _timer.Stop();
            _inputTimer.Stop();
            _leftRepeat.KeyUp();
            _rightRepeat.KeyUp();
            Status = "PAUSED";
        }
        else
        {
            _timer.Start();
            _inputTimer.Start();
            Status = string.Empty;
        }
    }

    /// <summary>エンジン操作後に呼び、バインド値の更新・ゲームオーバー判定・再描画通知を行う。</summary>
    private void AfterChange()
    {
        Score = _engine.Score;
        Lines = _engine.Lines;
        Level = _engine.Level;

        // 満杯行が揃ったら、落下を止めて消去アニメーションを再生してもらう。
        if (_engine.IsClearing)
        {
            _timer.Stop();
            _inputTimer.Stop();
            StateChanged?.Invoke(this, EventArgs.Empty); // 満杯のままの盤面を描画
            if (_engine.PendingClearRows.Count >= 4)
                _soundService.PlayTetris();
            else
                _soundService.PlayLineClear();
            LinesClearing?.Invoke(this, new LinesClearingEventArgs(_engine.PendingClearRows));
            return;
        }

        bool justEnded = false;
        bool newRecord = false;
        if (_engine.IsGameOver)
        {
            _timer.Stop();
            _inputTimer.Stop();
            Status = "GAME OVER\nEnter で再開";
            if (!_gameOverNotified)
            {
                _gameOverNotified = true;
                justEnded = true;
                _soundService.PlayGameOver();
                if (_engine.Score > _highScore)
                {
                    HighScore = _engine.Score;
                    _highScoreService.Save(_highScore);
                    newRecord = true;
                }
            }
        }

        // 先に最終盤面を描画させてから、ゲームオーバー演出を開始する。
        StateChanged?.Invoke(this, EventArgs.Empty);
        if (justEnded)
        {
            if (newRecord)
            {
                NewRecord?.Invoke(this, EventArgs.Empty);
            }
            GameOver?.Invoke(this, EventArgs.Empty);
        }
    }
}
