# 項目3: DAS/ARR（横移動の自作キーリピート）の追加

## 目的

現在、左右移動は `MainWindow.xaml` の `KeyBinding` に依存しており、OS のキーリピート設定（初回遅延・リピート間隔）に左右される。
自前で DAS（Delayed Auto Shift: 押下から自動リピート開始までの遅延）と ARR（Auto Repeat Rate: リピート間隔）を制御し、環境に依存しない一定の操作感にする。

## 変更方針

WPF に依存しない純粋なロジックとしてリピート制御を切り出し、xUnit でテスト可能にする。

- `src/Tetris/Input/AutoRepeatController.cs`（新規）
  - `KeyDown()` / `KeyUp()` でキー状態を通知
  - `Advance(TimeSpan elapsed)` で経過時間を進め、その間に発生させるべき「移動回数」を返す
  - 既定値: DAS = 170ms、ARR = 50ms（コンストラクタで上書き可能）
- `GameViewModel`
  - 左右それぞれに `AutoRepeatController` を1つずつ保持
  - `MoveLeftKeyDown()` / `MoveLeftKeyUp()` / `MoveRightKeyDown()` / `MoveRightKeyUp()` を追加。KeyDown 時は即座に1回移動し、コントローラーに `KeyDown()` を通知
  - 専用の高頻度タイマー（16ms 間隔）を追加し、Tick 毎に両コントローラーの `Advance()` を呼び、返ってきた回数だけ `MoveLeft`/`MoveRight` を実行する
  - このタイマーは既存の落下タイマーと同様に `Start()` / `TogglePause()` / `CompleteLineClear()` で開始・停止する
- `MainWindow.xaml` / `.xaml.cs`
  - 左右キーの `KeyBinding` を削除し、代わりにウィンドウの `PreviewKeyDown` / `PreviewKeyUp` で `Key.Left` / `Key.Right` を捕捉
  - `PreviewKeyDown` は `e.IsRepeat` が `true`（OS のキーリピート）の場合は無視し、独自のリピートに任せる

## 変更ファイル

- `src/Tetris/Input/AutoRepeatController.cs`（新規）
- `src/Tetris/ViewModels/GameViewModel.cs` — コントローラー保持、専用タイマー、KeyDown/KeyUp通知メソッド追加
- `src/Tetris/Views/MainWindow.xaml` — Left/Right の `KeyBinding` 削除
- `src/Tetris/Views/MainWindow.xaml.cs` — `PreviewKeyDown` / `PreviewKeyUp` ハンドラ追加
- `tests/Tetris.Tests/AutoRepeatControllerTests.cs`（新規）

## テスト項目（TDD、Red→Green→Refactorを1項目ずつ）

`AutoRepeatController` は WPF に依存しないため、xUnit で直接検証する。
`GameViewModel`/`MainWindow` の配線部分は本プロジェクトの既存方針（ViewModel/View 自体は自動テスト対象外）に倣い、ビルド成功と目視確認で担保する。

1. `Advance_BeforeKeyDown_ReturnsZero` — 何も押していない状態では 0 を返す
2. `Advance_ImmediatelyAfterKeyDown_BeforeDas_ReturnsZero` — KeyDown 直後、DAS 未満の経過では 0 を返す
3. `Advance_AfterDasElapsed_ReturnsAtLeastOneRepeat` — DAS 経過後は 1 回以上のリピートを返す
4. `Advance_MultipleArrIntervalsElapsed_ReturnsMultipleRepeats` — DAS 経過後、ARR の複数倍の時間が経過すると複数回のリピートを返す
5. `KeyUp_StopsFurtherRepeats` — KeyUp 後は Advance を呼んでも 0 を返す
6. `KeyDown_ResetsPreviousState` — 再度 KeyDown すると状態がリセットされ、再び DAS 待ちから始まる

## 影響範囲

- 左右移動の挙動が「OS キーリピート依存」から「固定 DAS/ARR」に変わる（体感の改善が目的の意図的な変更）。
- ハードドロップ・ソフトドロップ・回転などの一発系キーは `KeyBinding` のまま変更しない。
- WPF アプリの手動起動によるキー長押しの体感確認は、この環境では自動化できないため、ビルド成功・ロジックレビューでの確認に留める（実機確認が必要であればユーザー側でお願いする）。

## ブランチ名

`feature/das-arr`
