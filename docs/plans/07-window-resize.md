# 項目7: ウィンドウサイズ変更対応

## 目的

現在ウィンドウは `ResizeMode="CanMinimize"` で固定サイズ。可変サイズに対応し、高 DPI 環境や小さい画面でも快適に遊べるようにする。

## 変更方針

- `MainWindow.xaml` のルート `Window`:
  - `ResizeMode` を `CanMinimize` から `CanResize` に変更する。
  - `SizeToContent="WidthAndHeight"` は維持する（初期表示サイズは現状と変わらない。ユーザーが手動でリサイズした時点で WPF が自動サイズ調整を解除する標準動作を利用する）。
  - 縮小しすぎて操作不能にならないよう `MinWidth` / `MinHeight` を追加する。
- 盤面・サイドパネルを含む既存のルート `Grid`（`Margin="16"`）を `Viewbox`（`Stretch="Uniform"`）で包み、ウィンドウサイズ変更時にレイアウト全体が縦横比を保ったまま拡大縮小されるようにする。

## 変更ファイル

- `src/Tetris/Views/MainWindow.xaml` — `Window` の `ResizeMode`/`MinWidth`/`MinHeight`、`Viewbox` によるラップ

## テスト

- View（XAML/コードビハインド）は本プロジェクトの既存方針により自動テスト対象外。以下で確認する。
  - `dotnet build Tetris.sln` が警告・エラーなく成功すること。
  - `dotnet run --project src/Tetris` で起動し、ウィンドウをドラッグしてリサイズできること、縮小・拡大時に盤面とサイドパネルが崩れず縦横比を保って拡大縮小されることを目視確認する。

## 影響範囲

- ゲームロジック（`GameEngine`/`Tetromino`/`ViewModel`）への変更はなし。
- 既存の固定サイズ前提の見た目（初期表示）は変わらない。ユーザーがリサイズした場合のみ見た目が変化する。

## ブランチ名

`feature/window-resize`
