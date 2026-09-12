# 項目1: 逆回転（反時計回り）の追加

## 目的

現在は時計回り（↑キー）の回転のみ。反時計回りの回転を追加し、壁際や狭い隙間での配置自由度を向上させる。

## 変更方針

- `Tetromino.RotatedCcw()` を追加し、反時計回りに 90 度回転した新インスタンスを返す。
- `GameEngine` に `RotateCcw()` を追加。既存の `Rotate()`（時計回り）と同じウォールキック処理（その場→右→左→2マス）を共通化し、両メソッドから利用する。
- `GameViewModel` に `RotateCcwCommand` を追加し、効果音再生も既存の `Rotate()` と同様に行う。
- `MainWindow.xaml` に `Z` キーで `RotateCcwCommand` を割り当てる。
- 操作説明のテキストブロックに `Z : 逆回転` を追記する。

## 変更ファイル

- `src/Tetris/Models/Tetromino.cs` — `RotatedCcw()` 追加
- `src/Tetris/Game/GameEngine.cs` — `RotateCcw()` 追加、ウォールキック処理の共通化（`TryRotate` private helper）
- `src/Tetris/ViewModels/GameViewModel.cs` — `RotateCcwCommand` 追加
- `src/Tetris/Views/MainWindow.xaml` — `Z` キーバインド・操作説明追記
- `tests/Tetris.Tests/TetrominoTests.cs` — 反時計回りのテスト追加
- `tests/Tetris.Tests/GameEngineTests.cs` — `RotateCcw()` のテスト追加

## テスト項目（TDD、Red→Green→Refactorを1項目ずつ）

1. `RotatedCcw_TPiece_MatchesCounterClockwiseRotation` — T ピースを反時計回りに回転させた形状が期待値と一致する
2. `RotatedCcw_FourTimes_ReturnsToOriginalShape` — 4 回反時計回転で元の形状に戻る
3. `RotatedCcw_OPiece_KeepsSameShape` — O ピースは反時計回転しても形が変わらない
4. `RotateCcw_Succeeds_WhenSpaceAvailable` — 通常の空間で反時計回転が成功し、Current の形状が変わる
5. `RotateCcw_NearWall_UsesWallKick` — 壁際で回転がそのままでは失敗する位置から、キックにより回転が成功することを確認する

## 影響範囲

- 既存の時計回り回転（`Rotate()`）の外部動作は変えない（内部でヘルパーを共通化するのみ）。
- 新規キーバインド追加のみで UI の既存レイアウトへの影響はなし。

## ブランチ名

`feature/reverse-rotation`
