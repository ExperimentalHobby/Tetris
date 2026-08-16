using System.Diagnostics;
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
    private readonly AutoRepeatSettingsService _autoRepeatSettingsService = new();
    private AutoRepeatController _leftRepeat;
    private AutoRepeatController _rightRepeat;
    private readonly HighScoreService _highScoreService = new();
    private readonly SoundEffectService _soundService = new();
    private readonly Stopwatch _playStopwatch = new();

    private bool _isStarted;
    private bool _isPaused;
    private bool _gameOverNotified;

    private int _score;
    private int _lines;
    private int _level = 1;
    private int _highScore;
    private string _status = "Enter で開始";
    private TimeSpan _playTime;
    private int _pieceCount;
    private double _tetrisRate;
    private double _pps;
    private double _lpm;

    public GameViewModel()
    {
        _highScore = _highScoreService.Load();
        AutoRepeatSettings = _autoRepeatSettingsService.Load();
        _leftRepeat = new AutoRepeatController(AutoRepeatSettings.Das, AutoRepeatSettings.Arr);
        _rightRepeat = new AutoRepeatController(AutoRepeatSettings.Das, AutoRepeatSettings.Arr);
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

    /// <summary>現在のDAS/ARR設定（キーコンフィグと同様、DAS/ARR設定画面での表示・保存に使用）。</summary>
    public AutoRepeatSettings AutoRepeatSettings { get; private set; }

    /// <summary>一時停止中かどうか（キーコンフィグ画面表示時の自動ポーズ判定などに使用）。</summary>
    public bool IsPaused => _isPaused;

    /// <summary>経過プレイ時間（統計表示用）。プレイ中も逐次更新され、ゲームオーバー時点で確定する。</summary>
    public TimeSpan PlayTime { get => _playTime; private set => SetProperty(ref _playTime, value); }

    /// <summary>出現ピース総数（統計表示用）。プレイ中も逐次更新され、ゲームオーバー時点で確定する。</summary>
    public int PieceCount { get => _pieceCount; private set => SetProperty(ref _pieceCount, value); }

    /// <summary>テトリス率(%)（統計表示用）。プレイ中も逐次更新され、ゲームオーバー時点で確定する。</summary>
    public double TetrisRate { get => _tetrisRate; private set => SetProperty(ref _tetrisRate, value); }

    /// <summary>PPS（Pieces Per Second、統計表示用）。プレイ中も逐次更新され、ゲームオーバー時点で確定する。</summary>
    public double Pps { get => _pps; private set => SetProperty(ref _pps, value); }

    /// <summary>LPM（Lines Per Minute、統計表示用）。プレイ中も逐次更新され、ゲームオーバー時点で確定する。</summary>
    public double Lpm { get => _lpm; private set => SetProperty(ref _lpm, value); }

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
        OnPropertyChanged(nameof(IsPaused)); // ポーズ中に再スタートされた場合（StartCommandにCanExecute制限が無い）も盤面マスクを確実に解除する。
        _gameOverNotified = false;
        _playStopwatch.Restart();
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
        _engine.GravityDrop();
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

    /// <summary>
    /// DAS/ARR の経過を進め、リピート分の左右移動を行う専用タイマーの Tick。
    /// ロックディレイ（500ms）も落下タイマー（レベルに応じて80〜800ms）ではなくこの16ms間隔で進めることで、
    /// 設計値どおりの精度で判定する。
    /// </summary>
    private void OnInputTick(object? sender, EventArgs e)
    {
        if (_isPaused || _engine.IsGameOver || _engine.IsClearing)
        {
            return;
        }
        var interval = _inputTimer.Interval;
        bool locked = _engine.AdvanceLockDelay(interval);
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
        if (locked || leftRepeats > 0 || rightRepeats > 0)
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

    /// <summary>
    /// DAS/ARR設定を反映して永続化する。左右移動のリピート制御器を新しい設定値で作り直すため、
    /// 呼び出し時点でキーが押されっぱなしの状態はリセットされる（DAS/ARR設定画面はプレイ中なら自動ポーズする想定）。
    /// </summary>
    public void ApplyAutoRepeatSettings(AutoRepeatSettings settings)
    {
        _autoRepeatSettingsService.Save(settings);
        AutoRepeatSettings = settings;
        _leftRepeat = new AutoRepeatController(settings.Das, settings.Arr);
        _rightRepeat = new AutoRepeatController(settings.Das, settings.Arr);
    }

    private void ToggleMute() => IsMuted = !IsMuted;

    private void TogglePause()
    {
        if (!_isStarted || _engine.IsGameOver)
        {
            return;
        }
        _isPaused = !_isPaused;
        OnPropertyChanged(nameof(IsPaused)); // 盤面マスクオーバーレイの表示切り替え(XAMLバインディング)に必要。
        if (_isPaused)
        {
            _timer.Stop();
            _inputTimer.Stop();
            _playStopwatch.Stop();
            _leftRepeat.KeyUp();
            _rightRepeat.KeyUp();
            Status = "PAUSED";
        }
        else
        {
            _timer.Start();
            _inputTimer.Start();
            _playStopwatch.Start(); // Restart ではなく Start。ポーズ前の経過時間から計測を再開する。
            Status = string.Empty;
        }
    }

    /// <summary>エンジン操作後に呼び、バインド値の更新・ゲームオーバー判定・再描画通知を行う。</summary>
    private void AfterChange()
    {
        Score = _engine.Score;
        Lines = _engine.Lines;
        Level = _engine.Level;
        if (_isStarted)
        {
            UpdateLiveStats();
        }

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
                _playStopwatch.Stop();
                UpdateLiveStats(); // 停止時点の値で最終確定する。
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

    /// <summary>経過時間・ピース数・テトリス率・PPS・LPMを現在の状態から再計算して反映する（プレイ中も逐次呼ばれる）。</summary>
    private void UpdateLiveStats()
    {
        PlayTime = _playStopwatch.Elapsed;
        PieceCount = _engine.PieceCount;
        TetrisRate = _engine.TetrisRate;
        Pps = PlayStatsCalculator.PiecesPerSecond(_engine.PieceCount, PlayTime);
        Lpm = PlayStatsCalculator.LinesPerMinute(_engine.Lines, PlayTime);
    }
}
