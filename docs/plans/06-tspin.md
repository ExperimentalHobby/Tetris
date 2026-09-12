# 項目6: T-Spin判定と加点

## 目的

上級者向けの定番機能である T-Spin を実装する。判定には正確なウォールキック情報が必要なため、現在の簡易ウォールキック（`{0,1,-1,2,-2}` を X 方向にだけ試す暫定実装）を SRS（Super Rotation System）準拠のキックテーブルに置き換える。

## 変更方針

### 1. SRS 準拠ウォールキックへの置き換え（土台）

- `Tetromino` に `RotationState`（0=spawn, 1=R, 2=180, 3=L）を追加。`Rotated()`/`RotatedCcw()`/`Clone()` で保持・更新する。
- `GameEngine` の `TryRotate` を、JLSTZ 用・I 用それぞれの SRS キックテーブル（5 パターンずつ、遷移ペア 8 通り）に基づいて offset を試す実装に置き換える。O ピースはキック不要（従来通り (0,0) のみ）。
- 現在の実装は Y 方向のオフセットを一切試しておらず（コメントには「上」とあるが実装されていない）、SRS 化によって縦方向のキックも正しく機能するようになる。

### 2. T-Spin 判定

- 判定条件（簡易版・3 コーナールール）: **直前の成功アクションが回転であること** かつ **ピース種が T** かつ **ピース中心の 4 隅のうち 3 つ以上が壁または既存ブロックで埋まっていること**。
- Mini / Full の判別は「回転方向（ピースの尖端方向）側の 2 隅のうち何個埋まっているか」で行う簡易ルールを採用する（尖端側 2 隅とも埋まっていれば Full、片方のみなら Mini）。
  - **簡略化について**: 公式 SRS では「5 番目のキックテストで回転が成立した場合は無条件で Full」という特例があるが、今回はこの特例を実装せず、3 コーナールールのみで判定する。ごく一部のケースで公式とは異なる Mini/Full 判定になり得るが、実用上は問題ない。
- `GameEngine` に「直前の成功アクションが移動か回転か」を追う内部フラグを持たせる（移動成功で false、回転成功で true、ピース出現時に false へ初期化）。

### 3. スコアリング

- ライン消去を伴う T-Spin（`CommitClear` 内）: 通常のライン消去テーブルの代わりに専用テーブルを使う。
  - Full: 1 ライン=800 / 2 ライン=1200 / 3 ライン=1600（いずれも ×Level）
  - Mini: 1 ライン=200（×Level）
  - コンボボーナスは通常の消去と同様に加算する。
- ライン消去を伴わない T-Spin（`LockPiece` 内、`_pendingClear` が空の場合）: 固定点ボーナスを即加算する。
  - Full: 400×Level / Mini: 100×Level
  - この場合コンボは通常通り 0 にリセットする（消去を伴わないため）。
- Back-to-Back: 「テトリス」または「ライン消去を伴う T-Spin（Mini/Full 問わず）」を difficult な消去として扱い、直前も difficult だった場合に基礎点の +50% を加算する（項目5 で実装済みの B2B 判定を拡張）。

## 変更ファイル

- `src/Tetris/Models/Tetromino.cs` — `RotationState` 追加
- `src/Tetris/Game/GameEngine.cs` — SRS キックテーブル、T-Spin 判定・加点ロジック追加
- `tests/Tetris.Tests/TetrominoTests.cs` — `RotationState` のテスト追加
- `tests/Tetris.Tests/GameEngineTests.cs` — SRS キック・T-Spin のテスト追加

## テスト項目（TDD、Red→Green→Refactorを1項目ずつ）

1. `Rotated_UpdatesRotationState` — 時計回り回転で `RotationState` が 1 進む（4 回で 0 に戻る）
2. `RotatedCcw_UpdatesRotationState` — 反時計回り回転で `RotationState` が 1 戻る
3. `Rotate_IPiece_UsesIKickTable_NotJlstzTable` — I ピース特有の大きいキック（dx=-2）が使われることを確認し、JLSTZ 用テーブルと混同していないことを検証する
4. `TSpin_Full_ClearsOneLine_ScoresTSpinSingle` — 3 隅（尖端側2隅とも）が埋まった状態で回転により設置・1 ライン消去 → Full T-Spin Single 相当（800×Level）が加点される
5. `TSpin_Mini_NoLines_AddsFlatBonus` — 3 隅（尖端側1隅のみ）が埋まった状態で回転により設置・ライン消去なし → Mini の固定点ボーナス（100×Level）が即加算される
6. `TSpin_RequiresLastActionRotation_TranslationDoesNotCount` — 同じ3隅配置でも、回転ではなく直接配置（移動相当）で設置した場合は T-Spin と判定されず、通常のライン消去点のみになる

## 影響範囲

- 回転の当たり判定（ウォールキック）が SRS 準拠に変わるため、狭い隙間での回転挙動が従来と変わる可能性がある（既存の `Rotate`/`RotateCcw` の回転成功・盤面内収まりを確認するテストは変更なしで通る想定）。
- UI 表示は今回のスコープでは変更しない（T-Spin 専用のバナー等は追加しない。エンジンの判定・加点ロジックのみ）。
- Back-to-Back の対象が「テトリスのみ」から「テトリス or ライン消去を伴う T-Spin」に拡張される（項目5 の実装を拡張）。

## ブランチ名

`feature/tspin`
