# 項目9: 統計表示

## 目的

プレイ時間・ピース数・テトリス率などをゲームオーバー時に表示し、プレイ後の振り返りを楽しめるようにする。

## 変更方針

- `GameEngine`
  - `PieceCount`（出現したピースの総数）を追加。`SpawnPiece` 成功時に +1。
  - `TetrisCount`（テトリス＝4ライン同時消しの回数）、`TotalClearCount`（ライン消去を伴った固定の総回数）を追加。`CommitClear` 内で更新する。
  - `TetrisRate`（`TotalClearCount` が 0 のときは 0、それ以外は `TetrisCount / TotalClearCount * 100`）を計算プロパティとして追加。
  - `Start()` でこれらをリセットする。
- `GameViewModel`
  - `System.Diagnostics.Stopwatch` を用いて `Start()` でプレイ時間の計測を開始し、ゲームオーバー検出時（既存の `_gameOverNotified` 分岐）に停止する。
  - `PlayTime`（`TimeSpan`）、`PieceCount`（`int`）、`TetrisRate`（`double`）をバインド可能プロパティとして公開し、ゲームオーバー確定時に値を更新する。
- `MainWindow.xaml`
  - 既存の GAME OVER オーバーレイに、プレイ時間・ピース数・テトリス率を表示するテキストを追加する。

## 変更ファイル

- `src/Tetris/Game/GameEngine.cs` — `PieceCount`/`TetrisCount`/`TotalClearCount`/`TetrisRate` 追加
- `src/Tetris/ViewModels/GameViewModel.cs` — `Stopwatch` によるプレイ時間計測、バインド可能プロパティ追加
- `src/Tetris/Views/MainWindow.xaml` — GAME OVER オーバーレイへの統計表示追加
- `tests/Tetris.Tests/GameEngineTests.cs` — 統計値のテスト追加

## テスト項目（TDD、Red→Green→Refactorを1項目ずつ）

1. `Start_PieceCount_IsOne` — 開始直後、最初のピースが出現済みのため `PieceCount` が 1
2. `LockPiece_SpawningNextPiece_IncrementsPieceCount` — ピース固定→次ピース出現で `PieceCount` が増える
3. `CommitClear_TetrisClear_IncrementsTetrisCountAndTotalClearCount` — テトリスを決めると `TetrisCount`/`TotalClearCount` がともに 1 増える
4. `TetrisRate_ComputesPercentageOfTetrisClears` — 通常消去 1 回・テトリス 1 回の後、`TetrisRate` が 50（%）になる

`GameViewModel`/`MainWindow` のプレイ時間計測・表示部分は本プロジェクトの既存方針により自動テスト対象外。ビルド成功と目視確認で担保する。

## 影響範囲

- 既存のスコア・ライン・レベル等の挙動には影響しない（新規の集計プロパティを追加するのみ）。
- GAME OVER 画面の表示内容が増える（レイアウトが少し変わる）。

## ブランチ名

`feature/stats`
