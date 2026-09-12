# 項目4: NEXT の複数表示（3個）

## 目的

現在 NEXT は 1 個のみ表示。7-bag のキューを活用し、直近 3 個の先読み表示に拡張して戦略性を高める。

## 変更方針

- `GameEngine`
  - 内部に `_nextQueue`（`List<TetrominoType>`、先頭が直近の次ピース）を持ち、常に `PreviewCount`（3）個を維持する。
  - 既存の `NextType`（単一の次ピース）は `_nextQueue[0]` を返す互換プロパティとして残す（既存テスト・UI コードへの影響を避けるため）。
  - `NextQueue`（`IReadOnlyList<TetrominoType>`）を新規公開し、先読み全体を返す。
  - `SpawnNext()` は `_nextQueue` の先頭を取り出してピース生成し、末尾に `NextFromBag()` で 1 個補充する。
  - `Start()` は `_nextQueue` を `PreviewCount` 個まで満たしてから最初のピースを出す。
- `MainWindow.xaml` / `.xaml.cs`
  - NEXT 表示欄（`NextCanvas`）を縦に 3 枠分の高さへ拡張。
  - `DrawNext()` を `NextQueue` の各要素をそれぞれの枠に描画するよう変更（既存の単一プレビュー描画ロジックを、矩形範囲を指定できる形に共通化）。
  - HOLD 表示（`DrawHold`）は変更なし（共通化した描画処理を流用するのみ）。

## 変更ファイル

- `src/Tetris/Game/GameEngine.cs` — `NextQueue` 追加、`SpawnNext`/`Start` の変更
- `src/Tetris/Views/MainWindow.xaml` — `NextCanvas` の高さ変更
- `src/Tetris/Views/MainWindow.xaml.cs` — `DrawNext` を複数枠描画に変更、プレビュー描画の共通化
- `tests/Tetris.Tests/GameEngineTests.cs` — `NextQueue` の挙動テスト追加

## テスト項目（TDD、Red→Green→Refactorを1項目ずつ）

1. `Start_NextQueue_HasThreePreviewItems` — 開始直後、`NextQueue` の要素数が 3 であることを確認する
2. `NextQueue_FirstItem_MatchesNextType` — `NextQueue[0]` が `NextType` と一致することを確認する
3. `SpawnNext_ConsumesQueueFrontAndRefillsTail` — ピース確定（固定→次ピース出現）後も `NextQueue` の要素数は 3 を維持し、新しい次ピースが以前の `NextQueue[1]` と一致することを確認する
4. `NextQueue_Contains7BagPieces_WithoutImmediateDuplicatesAcrossBagBoundary` — 既存の 7-bag 制約（1 巡に同じ種が重複しない）が、キュー越しに複数個取り出しても壊れないことを確認する（例: 21 回分ピースを進めて全種の出現回数を検証）

`MainWindow` の描画変更はビルド成功で確認する（本プロジェクトの既存方針により View 自体は自動テスト対象外）。

## 影響範囲

- `NextType` の外部挙動は変えない（計算プロパティ化のみ）。
- NEXT 表示領域の見た目（高さ）が変わる。

## ブランチ名

`feature/next-queue`
