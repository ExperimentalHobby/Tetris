# 項目2: ロックディレイ（接地猶予）の追加

## 目的

現在は接地した瞬間に `SoftDrop()` が即座にピースを固定する（`GameEngine.cs` 旧 127-136 行目）。
接地後 500ms の猶予を設け、その間の移動・回転で再び固定タイミングをリセットできるようにし、
高レベル時の操作感を改善する。無限に固定を回避できないよう、リセット回数の上限（15回）も設ける。

## 変更方針

`GameEngine` は現在ウォールクロックを持たず、`ViewModel` の `DispatcherTimer` から駆動される。
この構造を踏襲し、ロックディレイの経過時間管理も **`GameEngine` に経過時間を渡して進める方式** にする。

- `GameEngine`
  - `IsGrounded`（現在のピースがこれ以上下に動けないか）を公開。
  - `SoftDrop()` を変更: 接地時に **即座に固定しない**。移動できた場合のみ従来通り加点。
  - `AdvanceLockDelay(TimeSpan elapsed)` を追加: 接地中のみ経過時間を積算し、500ms（`LockDelayDuration`）に達したら固定する。非接地なら経過時間をリセットする。
  - 移動（`TryMove`）・回転（`TryRotate`）が成功した際の共通処理として、接地中なら経過時間をリセットしリセット回数をカウント（`MaxLockResets` = 15 に到達したらそれ以上リセットしない＝いずれ固定される）。
  - `Start()` / 新ピース出現時に経過時間・リセット回数を初期化。
- `GameViewModel`
  - `OnTick`（重力タイマー）で `SoftDrop()` の後に `AdvanceLockDelay(_engine.DropInterval)` を呼ぶ。
  - 手動ソフトドロップ（キー押下）ではロックディレイを進めない（重力ティックのみが接地時間を積算する）。ハードドロップは従来通り即固定（変更なし）。

## 変更ファイル

- `src/Tetris/Game/GameEngine.cs` — `IsGrounded`、`AdvanceLockDelay`、共通リセット処理の追加、`SoftDrop()` の挙動変更
- `src/Tetris/ViewModels/GameViewModel.cs` — `OnTick` に `AdvanceLockDelay` 呼び出しを追加
- `tests/Tetris.Tests/GameEngineTests.cs` — ロックディレイの挙動テスト追加

## テスト項目（TDD、Red→Green→Refactorを1項目ずつ）

1. `SoftDrop_WhenGrounded_DoesNotLockImmediately` — 接地位置で `SoftDrop()` しても即座には固定されない
2. `AdvanceLockDelay_BeforeDelayElapsed_DoesNotLock` — 500ms未満の経過では固定されない
3. `AdvanceLockDelay_WhileGrounded_LocksAfterDelay` — 500ms経過すると固定され、Grid にブロックが反映される
4. `AdvanceLockDelay_WhileNotGrounded_DoesNotAccumulate` — 接地していない間は時間経過があっても固定されない
5. `MoveLeft_WhileGrounded_ResetsLockDelay` — 接地中に横移動すると経過時間がリセットされ、合算しても固定されない
6. `LockDelay_MaxResetsExceeded_LocksDespiteContinuedMovement` — リセット回数の上限（15回）を超えると、移動を続けても最終的に固定される

## 影響範囲

- `SoftDrop()` の外部挙動が変わる（接地時に即固定しなくなる）。既存テスト `SingleLineClear_IsDetectedThenCommittedWithScore` 等は `SetCurrentForTest`/`LockCurrentForTest` を使う決定的セットアップのため影響なし。
- ハードドロップの挙動は変更なし（従来通り即固定）。
- 自然落下（重力ティック）のみがロックディレイを進める。手動ソフトドロップの連打では固定を早められない（今回のスコープでは対象外）。

## ブランチ名

`feature/lock-delay`
