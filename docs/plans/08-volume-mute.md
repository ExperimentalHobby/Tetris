# 項目8: 音量設定・ミュート切替

## 目的

効果音は実装済み（`SoundEffectService`）。音量調整とミュート切替を追加し、ユーザーが好みに合わせて調整できるようにする。

## 変更方針

- `SoundEffectService`
  - `Volume`（`double`, 既定 1.0, 0.0〜1.0 にクランプ）を追加。
  - `IsMuted`（`bool`, 既定 false）を追加。
  - `Play()` 内で `MediaPlayer.Volume` に `IsMuted ? 0.0 : Volume` を設定してから再生する。
- `GameViewModel`
  - `Volume`（バインド可能プロパティ、`_soundService.Volume` に委譲）、`IsMuted`（同様に委譲）を追加。
  - `ToggleMuteCommand`（`IsMuted` を反転する `RelayCommand`）を追加。
- `MainWindow.xaml`
  - `M` キーで `ToggleMuteCommand` を実行できるようにする。
  - サイドパネルに音量スライダー（`Volume` にバインド、0.0〜1.0）とミュート状態表示を追加する。
  - 操作説明に `M : ミュート切替` を追記する。

## 変更ファイル

- `src/Tetris/Services/SoundEffectService.cs` — `Volume`/`IsMuted` 追加、`Play()` の変更
- `src/Tetris/ViewModels/GameViewModel.cs` — `Volume`/`IsMuted`/`ToggleMuteCommand` 追加
- `src/Tetris/Views/MainWindow.xaml` — `M` キーバインド、音量スライダー、操作説明追記
- `tests/Tetris.Tests/SoundEffectServiceTests.cs` — `Volume`/`IsMuted` のテスト追加

## テスト項目（TDD、Red→Green→Refactorを1項目ずつ）

1. `Volume_DefaultsToOne` — 新規インスタンスの `Volume` が 1.0
2. `Volume_ClampsToValidRange` — 1.5 を設定すると 1.0 に、-0.5 を設定すると 0.0 にクランプされる
3. `IsMuted_DefaultsToFalse` — 新規インスタンスの `IsMuted` が false
4. `PlayMethods_AfterChangingVolumeAndMute_DoNotThrow` — 音声ファイルが存在しない環境で `Volume`/`IsMuted` を変更した後に各 `Play` メソッドを呼んでも例外が発生しない（既存の堅牢性テストの拡張）

`GameViewModel`/`MainWindow` の配線部分は本プロジェクトの既存方針により自動テスト対象外。ビルド成功と目視確認で担保する。

## 影響範囲

- 既存の効果音再生タイミング・種類は変更しない（音量とミュートの制御のみ追加）。
- UI にスライダーとミュート表示が追加される（サイドパネルのレイアウトが少し変わる）。

## ブランチ名

`feature/volume-mute`
