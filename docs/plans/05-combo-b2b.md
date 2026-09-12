# 項目5: コンボ（REN）と Back-to-Back ボーナスの追加

## 目的

連続したライン消去（コンボ）や連続テトリス（Back-to-Back）に加点することで、スコアシステムに深みを持たせる。

## 変更方針

`GameEngine.CommitClear()`（実際の消去確定・加点処理）と `LockPiece()`（ピース固定・満杯行検出）にスコアリングロジックを追加する。

- **コンボ（REN）**
  - `public int Combo { get; private set; }` を追加。連続してライン消去が成功するたびに +1、消去を伴わない固定が起きた時点で 0 にリセットする。
  - ボーナス計算: `50 * (Combo - 1) * Level`（同一ストリーク内の 1 回目の消去はボーナス 0、2 回目以降から加算される、一般的なガイドライン準拠の式）。
- **Back-to-Back（B2B）**
  - `public bool IsBackToBack { get; private set; }` を追加。
  - 現時点では「テトリス（4 ライン同時消し）」のみを "difficult" な消去として扱う（T-Spin は項目6で追加予定。追加時にこの判定を拡張する）。
  - 直前の消去も difficult だった場合、今回の消去に対して基礎点の +50% をボーナスとして加算する。消去を伴わない固定（空振り）は B2B のストリークを途切れさせない（ガイドライン準拠）。
- `Start()` で `Combo` / `IsBackToBack` / 内部の直前消去種別フラグを初期化する。

## 変更ファイル

- `src/Tetris/Game/GameEngine.cs` — `Combo`/`IsBackToBack` の追加、`CommitClear`/`LockPiece`/`Start` の変更
- `tests/Tetris.Tests/GameEngineTests.cs` — コンボ・B2B のテスト追加

## テスト項目（TDD、Red→Green→Refactorを1項目ずつ）

1. `CommitClear_SingleClear_ComboBecomesOneWithNoBonus` — 単発のライン消去では `Combo` が 1 になり、コンボボーナスは加算されない
2. `CommitClear_ConsecutiveClears_ComboIncrementsAndAddsBonus` — 消去を伴う固定が連続すると `Combo` が 2 になり、2 回目の消去にコンボボーナスが加算される
3. `LockPiece_WithoutClearingLines_ResetsComboToZero` — ライン消去を伴わない固定が起きると `Combo` が 0 にリセットされる
4. `CommitClear_TetrisFollowedByTetris_AddsBackToBackBonus` — テトリスに続けてテトリスを決めると 2 回目に `IsBackToBack` が true になり、基礎点の +50% が加算される
5. `CommitClear_EasyClearBreaksBackToBack` — テトリス → 1 ライン消去 → テトリスの順では、3 回目のテトリスに B2B ボーナスが付かない（間の 1 ライン消去でストリークが途切れる）

## 影響範囲

- 既存のスコア計算（`SingleLineClear_IsDetectedThenCommittedWithScore` 等）は初回消去時にコンボ・B2B ボーナスが 0 のため、既存のスコア期待値に影響しない。
- UI 表示は今回のスコープでは変更しない（エンジンの加点ロジックのみ）。将来的に UI へコンボ表示を追加する余地は残す。

## ブランチ名

`feature/combo-b2b`
