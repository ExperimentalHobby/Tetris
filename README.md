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
| ↑ | 回転（時計回り） |
| Z | 回転（反時計回り） |
| ↓ | ソフトドロップ |
| Space | ハードドロップ |
| C | ホールド |
| Enter | 開始 / リスタート |
| P | 一時停止 |
| M | ミュート切替 |

キー割り当ては画面上の KEY CONFIG ボタンから変更できる。横移動の自動リピート（DAS/ARR）も同ボタンから調整可能。

## 機能

- 7-bag 方式によるピース抽選、NEXT ピースの複数先読み表示
- ゴースト（着地予測）表示、ホールド
- ロックディレイ（接地後の猶予時間内は移動・回転で置き直し可能）
- DAS/ARR を自前のタイマーで制御する横移動の自動リピート
- ライン消去数に応じたスコアリング、レベルに応じた落下速度の変化
- コンボ（連続ライン消去）・Back-to-Back ボーナス
- T-Spin 判定と加点
- プレイ時間・PPS（Pieces Per Second）・LPM（Lines Per Minute）などの統計表示
- 効果音（音量調整・ミュート切替、設定は永続化）
- ハイスコアの保存
- キーコンフィグ（キー割り当てのリマップ、DAS/ARR 調整）

## ファイル構成

```
Tetris.sln                             ソリューション（VS 2026 で開く）
build.bat                              ビルド用バッチ
src/Tetris/
  Tetris.csproj                        プロジェクト
  App.xaml / App.xaml.cs               アプリケーションエントリポイント
  AssemblyInfo.cs
  Models/     Tetromino.cs             テトロミノの形状・色・回転
  Game/       GameEngine.cs            盤面・ゲーム進行ロジック（描画とは独立）
  Input/      GameAction.cs            ゲーム操作の列挙
              KeyBindings.cs           操作とキーの対応（リマップ・永続化の対象）
              KeyDisplay.cs            キーの表示名変換
              AutoRepeatSettings.cs    DAS/ARR 設定値
              AutoRepeatController.cs  横移動の自動リピート制御
  Services/   HighScoreService.cs      ハイスコアの永続化
              KeyBindingService.cs     キー割り当ての永続化
              SoundEffectService.cs    効果音の再生
              SoundSettings.cs         音量・ミュート設定値
              SoundSettingsService.cs  音量・ミュート設定の永続化
              AutoRepeatSettingsService.cs  DAS/ARR 設定の永続化
  ViewModels/ GameViewModel.cs         状態表示・入力コマンド・ゲームループの駆動
              ObservableObject.cs      INotifyPropertyChanged 基底
              RelayCommand.cs          ICommand 実装
              PlayStatsCalculator.cs   PPS/LPM 等の統計計算
              LinesClearingEventArgs.cs ライン消去イベント引数
  Views/      MainWindow.xaml / .cs        UI レイアウトと描画・入力処理
              KeyConfigWindow.xaml / .cs   キーコンフィグ画面
              AutoRepeatConfigWindow.xaml / .cs  DAS/ARR 設定画面
```
