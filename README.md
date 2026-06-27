# Tetris

C# / .NET 10 / WPF で作ったテトリスです。

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## 動作環境

- Windows
- [.NET 10 SDK](https://dotnet.microsoft.com/)

## 実行方法

```bash
dotnet run
```

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

| ファイル | 役割 |
| --- | --- |
| `Tetromino.cs` | テトロミノの形状・色・回転 |
| `GameEngine.cs` | 盤面・ゲーム進行ロジック（描画とは独立） |
| `MainWindow.xaml` / `.cs` | UI レイアウトと描画・入力処理 |
