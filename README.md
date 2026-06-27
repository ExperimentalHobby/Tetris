# Tetris

C# / .NET 10 / WPF で作ったテトリスです。

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## 動作環境

- Windows
- [.NET 10 SDK](https://dotnet.microsoft.com/)

## 実行方法

```bash
dotnet run --project src/Tetris        # 実行
build.bat                              # ビルドのみ（Release 構成）
```

Visual Studio 2026 の場合はルートの `Tetris.sln` を開いてビルド・実行できます。

## 操作方法

| キー | 操作 |
| --- | --- |
| ← → | 左右移動 |
| ↑ | 回転 |
| ↓ | ソフトドロップ |
| Space | ハードドロップ |
| Enter | 開始 / リスタート |
| P | 一時停止 |

## 機能

- 7-bag 方式によるピース抽選
- ゴースト（着地予測）表示
- ライン消去数に応じたスコアリング、レベルに応じた落下速度の変化
- NEXT ピースのプレビュー

## ファイル構成

```
Tetris.sln                         ソリューション（VS 2026 で開く）
build.bat                          ビルド用バッチ
src/Tetris/
  Tetris.csproj                    プロジェクト
  App.xaml / App.xaml.cs           アプリケーションエントリポイント
  AssemblyInfo.cs
  Models/  Tetromino.cs            テトロミノの形状・色・回転
  Game/    GameEngine.cs           盤面・ゲーム進行ロジック（描画とは独立）
  Views/   MainWindow.xaml / .cs   UI レイアウトと描画・入力処理
```
