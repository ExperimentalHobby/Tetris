# CLAUDE.md

このファイルは、本リポジトリで作業する Claude Code (claude.ai/code) へのガイダンスを提供します。

## プロジェクト概要

**C# / .NET 10 / WPF** で実装したテトリスゲーム。

- **UI / 描画 / 入力**: WPF（`Canvas` 上に `Rectangle` を描画、`DispatcherTimer` でゲームループ）
- **ゲームロジック**: 描画から独立したプレーンな C# クラス
- 外部 NuGet 依存なし（.NET / WPF フレームワークのみ）

ターゲットフレームワークは `net10.0-windows`（Windows 専用）。

## ビルドと実行

```bash
dotnet build Tetris.sln            # ビルド
dotnet run --project src/Tetris    # 実行
dotnet test Tetris.sln             # テスト（xUnit）
build.bat                          # ビルド用バッチ（Release。引数で構成指定可）
```

VS 2026 はルートの `Tetris.sln` を開く。プロジェクト本体は `src/Tetris/`、テストは `tests/Tetris.Tests/`。

## テスト

- **xUnit**。WPF 非依存の `GameEngine` / `Tetromino` のロジックを検証する（`tests/Tetris.Tests/`）
- `Tetromino` が `Color` を公開しているため、テストプロジェクトも `net10.0-windows` + `UseWPF`
- 決定的な盤面を組むためのテストシームを `internal` で用意し、`Tetris.csproj` の `InternalsVisibleTo` で `Tetris.Tests` に公開している（`GameEngine.SetCurrentForTest` / `LockCurrentForTest`）
- 新機能・バグ修正では Red → Green → Refactor を徹底する（後述の作業ルール参照）

## アーキテクチャ

**ハイブリッド MVVM** 構成。状態表示と入力は MVVM（バインディング/コマンド）、盤面描画は性能上の理由でコードビハインドが担う。

| 層 | ファイル | 役割 |
| --- | --- | --- |
| Model | `src/Tetris/Models/Tetromino.cs` | テトロミノの形状定義・色・回転（正方行列の回転で姿勢を表現） |
| Model | `src/Tetris/Game/GameEngine.cs` | 盤面（10×20）とゲーム進行。落下・移動・回転・ライン消去・スコア・レベル・7-bag 抽選・ゴースト位置。UI から独立 |
| ViewModel | `src/Tetris/ViewModels/GameViewModel.cs` | スコア/ライン/レベル/状態をバインド公開、入力を `ICommand` 化、`DispatcherTimer` でゲームループを駆動。状態変化を `StateChanged` イベントで View に通知 |
| ViewModel | `src/Tetris/ViewModels/ObservableObject.cs` | `INotifyPropertyChanged` 基底（`SetProperty`） |
| ViewModel | `src/Tetris/ViewModels/RelayCommand.cs` | `ICommand` 実装 |
| View | `src/Tetris/Views/MainWindow.xaml` / `.cs` | レイアウト・バインディング・`InputBindings`（キー→コマンド）。コードビハインドは Canvas 描画のみ |
| - | `src/Tetris/App.xaml` / `.cs` | アプリケーションエントリポイント |

### 主要な設計ポイント

- **ロジックと描画の分離**: `GameEngine` は WPF 型に依存しない。テストや UI 差し替えがしやすい
- **MVVM の境界**: 入力は `MainWindow.xaml` の `InputBindings` から `GameViewModel` のコマンドへ。スコア等は `{Binding}`。盤面は `GameViewModel.StateChanged` を受けて `MainWindow.xaml.cs` が `Engine` を読み取り Canvas に再描画（200 セル超の毎フレーム描画をバインディングで行うのは非効率なため、ここだけコードビハインド）
- **座標系**: `(X=列, Y=行)`。盤面は `Grid[row, col]`（`TetrominoType?`、null が空セル）
- **回転**: `Tetromino.Rotated()` が時計回り 90 度回転した新インスタンスを返す。`GameEngine.Rotate()` が簡易ウォールキック（その場→右→左→2マス）を試行する
- **ピース抽選**: `GameEngine` は 7-bag 方式（7 種を 1 巡ずつシャッフルして配る）
- **落下速度**: `DropInterval` がレベルに応じて短くなる（最小 80ms）
- **スコア**: ライン消去数に応じた加点（1/2/3/4 = 100/300/500/800）にレベル補正。ソフト/ハードドロップにもボーナス
- **コメント規約（XML doc）**: `public` / `internal` メンバーには `<summary>` を記載する

---

## 作業ルール

- **実装前にプランを提示すること**: コード変更を行う前に、方針・変更箇所・影響範囲を日本語でまとめたプランを提示し、ユーザーの承認を得てから実装に進む。
- **承認されたプランは `docs/{機能名}-Plan.md` として書き出しておく**（`docs/` は `.gitignore` 対象のローカル資料であり PR には含まれない。実装の経緯を後から追えるようにする）。
- **複数項目をまとめて依頼された場合は、1項目ずつ「プラン提示→承認→TDD実装→ビルド/テスト確認→コミットメッセージ提示→承認→コミット・PR作成」のサイクルを完了させてから次の項目に進む。** TodoWrite で全項目の進捗を管理し、項目間で状態を見失わないようにする。
- **軽微な修正は直接編集可**: 以下の軽微な修正はプラン提示なしで直接編集してよい。ただし変更内容が明らかな場合のみ:
  - スペルミス・タイポ修正
  - 単純な値の誤り（符号違い `+`/`-`、定数値の訂正など）
  - コメント・ドキュメントの文言修正（既存ドキュメントの改善）
  - IDE 警告削除（未使用 using、未使用フィールドなど）

  ただし、以下のような変更は軽微ではないため、プラン提示後に実装すること: ロジック変更が伴う修正、ファイル追加・削除、テスト追加、依存関係の変更
- **GitHub Flow に従うこと**: コード変更は必ず feature ブランチで行い、`main` に直接コミットしない。

  **ブランチ運用手順（GitHub Flow）**

  1. **ブランチ作成** — 作業開始前に `main` から feature ブランチを切る。ブランチ名は作業内容を端的に示す（例: `feature/hold-piece`、`fix/rotation-wallkick`、`docs/readme`）
     ```bash
     git switch main
     git pull
     git switch -c feature/<作業名>
     ```
  2. **コミット** — 小さい単位でこまめにコミットする。コミットメッセージは変更の「なぜ」を説明する
  3. **プルリクエスト** — 実装が完了したら `main` への PR を作成する。PR タイトルは 70 文字以内、本文に変更内容・テスト方法を記載する
  4. **マージ後の後始末** — マージ後はローカル・リモートともにブランチを削除する
     ```bash
     git switch main && git pull
     git branch -d feature/<作業名>
     ```

  **禁止事項**
  - `main` ブランチへの直接コミット（緊急の typo 修正など極めて軽微なものを除く）
  - `git push --force` を `main` ブランチに対して実行すること
  - **ユーザーの明示的な指示なしにコミットを実行すること** — 実装完了後はコミットメッセージ案を提示してユーザーの承認を得てからコミットする。「コミットしてください」「commit して」などの明示的な指示があった場合のみ実行する

  **コミットメッセージの書き方**
  - コミットメッセージは**日本語**で記述する
  - 型プレフィックス（`feat`/`fix`/`docs`/`refactor`/`test` など）は英語のままでよい
  - 例: `feat: ホールド機能を追加`、`fix: 回転時のウォールキック不具合を修正`、`docs: README を追加`

- **テスト駆動開発を意識すること**: 新機能追加・バグ修正の際は、Red → Green → Refactor のサイクルを厳守する。

  **Red → Green → Refactor サイクル**

  1. **TODO リスト作成（実装前）** — 実装に入る前に、何をテストするかをリストアップする。コーディング中は視野が狭くなるため、冷静な状態で考える。
  2. **テスト記述** — 1 サイクルで倒すテストは必ず 1 つ。複数テストを同時に通そうとしない。
  3. **Red 確認（必須）** — テストを実行して失敗することを確認する。「期待したエラーメッセージ」であることまで検証してから次に進む。
  4. **Green（最短経路で合格）** — まず動くコードを書く。一時的な汚さは許容し、リファクタリングは次のフェーズに回す。
  5. **Refactor（外部ふるまいを変えず内部品質を上げる）** — テストが Green の状態を保ちながら実施する。こまめにテストを実行し、Red になったら直前の変更を見直す。時間制限を設けて終わりを決める。
  6. **繰り返し** — TODO リストから完了項目を消し、次のテストに進む。

  **アンチパターン（やってはいけないこと）**
  - 複数テストを同時に倒そうとする
  - Red 確認をスキップして実装に入る
  - Refactor 中にテスト実行を怠る
  - Refactor に時間制限を設けず終わりのないリファクタリングを続ける

- **作業完了時の要件**: 作業を終了する際は、以下の状態で完了すること。これらの要件を満たさずに作業を終了しない
  1. **ビルドエラーがないこと** — `dotnet build Tetris.sln` が成功すること
  2. **警告がないこと** — 未使用 using・未使用フィールドなど、コンパイラ/IDE の警告が出ていないこと
  3. **テストが全てパスしていること** — `dotnet test Tetris.sln` で全テストが成功すること
  4. **コミット・プッシュが完了していること** — ただし**ユーザーの明示的な指示がない限りコミットは実行しない**。コミット可能な状態まで整えたうえでコミットメッセージ案を提示する

## よくある落とし穴

1. **無関係な既存バグを独断で処理してしまう**: 作業中に今のタスクと無関係な既存バグを見つけた場合、黙って直す・黙って無視するのではなく、見つけた時点でユーザーに扱い（今の PR に含めるか、別 PR に分けるか）を確認してから進めること
2. **改行コードの混入確認漏れ**: 既存ファイルを編集した後は `git diff --stat` で差分行数を確認すること。編集ツールが意図せず LF/CRLF を変換すると、実際の変更が数行でもファイル全体が差分になってしまう。混入していたら `.gitattributes` の規約どおりの改行コードに戻してから再度確認する
