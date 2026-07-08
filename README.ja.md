# FileHistoryClone

![CI](https://github.com/tomoyukioya/FileHistoryClone/actions/workflows/ci.yml/badge.svg)
![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![Platform: Windows 10/11](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6)
![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)

[English README is here](README.md)

**すべてのファイルのすべてのバージョンを自動でバックアップ。しかもエクスプローラーでそのまま開ける通常ファイルとしてバックアップ。**

FileHistoryClone は、Windows「ファイル履歴」の思想を受け継ぐタスクトレイ常駐型バックアップツールです。指定フォルダを監視し、ファイルが変更されるたびにタイムスタンプ付きのコピーを保存します。3日前にうっかり上書きした文書も、ミスの前のバージョンを選んで復元するだけです。

- 🕘 **スケジュール不要の自動バックアップ** — 変更をリアルタイムに検知。バックアップ実施忘れがありません
- 📂 **独自形式なし** — バックアップはすべて通常ファイル(`report(2026_07_07 09_30_00).docx`)。本アプリがなくても直接開けます
- 🪶 **作業の邪魔をしない** — 重いスキャンは PC のアイドル時のみ・最低優先度スレッドで実行
- 🔒 **100% ローカル&プライベート** — クラウドなし、アカウント不要、サブスクなし、テレメトリなし

![FileHistoryClone の動作: 壊してしまったファイルを、自動保存された過去のバックアップから復元](docs/images/demo.gif)

## クイックスタート

**方法 A — ダウンロード:** [Releases](../../releases) からインストーラ(`FileHistoryCloneSetup-*.exe`、ユーザー単位・管理者不要)を実行 — または portable な zip(`-standalone` 版は .NET ランタイム不要)を展開して `FileHistory.exe` を実行。

**方法 B — ソースからビルド:**

```powershell
git clone https://github.com/tomoyukioya/FileHistoryClone.git
cd FileHistoryClone
dotnet run --project FileHistory\FileHistory.csproj -c Release
```

インストーラからインストールした場合、アプリ本体は`%LOCALAPPDATA%\Programs\FileHistoryClone`に配置されます。
起動後はタスクトレイに常駐し、初期状態では `ドキュメント` フォルダを `%USERPROFILE%\FileHistoryCloneBackup` に世代保存します。対象フォルダの変更や各種バックアップ設定はアプリ本体が配置されているフォルダにある [appsettings.json](FileHistory/appsettings.json) を編集してください(コメント付きの詳細な例は [appsettings.example.json](FileHistory/appsettings.example.json) を参照)。

## 開発の動機

Windows 8 で導入された[ファイル履歴 (File History)](https://support.microsoft.com/ja-jp/windows/windows-%E3%81%AE%E3%83%95%E3%82%A1%E3%82%A4%E3%83%AB%E5%B1%A5%E6%AD%B4-5de0e203-ebae-05ab-db85-d5aa0a199255) は、ドライブを指定するだけで文書の全バージョンが自動的に保存される素晴らしい機能でした。一度設定すると、以降バックアップスケジュールを意識する必要がありませんでした。しかし Microsoft は事実上メンテナンスを止めており、設定アプリからは項目が削除されてOneDriveへの誘導に置き換えられ、以前からの不具合(バックアップが静かに止まる、ファイルが通知なくスキップされる等)も修正されないまま、Windows における将来も不透明です。

FileHistoryClone は、このアイデアを独立したオープンソースツールとして残す試みです。継続的な世代管理付きローカルバックアップを通常ファイルとして保存し、独自コンテナにもクラウドにもサブスクリプションにも依存しません。

## 特徴

- **リアルタイムバックアップ** — `FileSystemWatcher` でファイルの作成・変更を検知し、自動的にバックアップをスケジュールします。
- **バックグラウンドクローリング** — 監視イベントの取りこぼしに備えて、対象フォルダを定期的にフルスキャンします。クローリングは PC のアイドル時のみ・最低優先度スレッドで実行されるため、通常の作業を妨げません。
- **世代管理** — バックアップは `元ファイル名(yyyy_MM_dd HH_mm_ss).拡張子` という通常ファイルとして、バックアップ先にディレクトリ構造ごとミラーされます。独自形式ではないので、エクスプローラーから直接開けます。
- **カタログデータベース** — ファイル・世代のメタデータは組み込みの [LiteDB](https://www.litedb.org/) で管理します。
- **復元 UI** — ツリーでバックアップ済みフォルダを辿り、ファイルの任意の世代を選んで復元できます(ディレクトリ単位の復元、タイムスタンプ保持に対応)。
- **保持ポリシー** — 1ファイルあたりの最大世代数(`MaxGenerations`)や保持日数(`RetentionDays`)を設定すると、古い世代を自動削除します。最新世代は常に保持されます。
- **手動クリーンアップ** — 「全ての最新のみ残す」「既存ファイルの最新のみ残す」の2モードをワンクリックで実行できます。
- **柔軟なフィルタ** — ディレクトリごとのバックアップ間隔、glob 形式の除外パターン(`.git`、`*.tmp`、`C:\Users\me\AppData` など)、除外の例外指定(`!important.log`)、パス中の環境変数展開(`%USERPROFILE%\Documents`)に対応。
- **多言語 UI** — 日本語・英語を同梱。OS の言語に自動追従し、`Language` 設定で固定もできます。

## 動作環境

- Windows 10/11
- ビルドには [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 以降

## トレイアイコンの操作

起動するとタスクトレイに常駐します。トレイアイコンの右クリックメニュー:

| メニュー | 動作 |
| --- | --- |
| 開く | 復元ウィンドウを開く(ダブルクリックでも可) |
| 設定を開く | `appsettings.json` をエディタで開く |
| Windows 起動時に実行 | ログオン時の自動起動を切り替え |
| 終了 | アプリケーションを終了 |

## 設定 (`appsettings.json`)

**保護するフォルダを変更するには** `appsettings.json` を編集します。最も簡単なのはトレイアイコン →**「設定を開く」**(インストーラ版ならスタートメニューの**「Edit FileHistoryClone settings」**でも可)。編集後は**アプリを再起動**(トレイ→終了→もう一度起動)すると反映されます。

ファイルの場所:

| 実行方法 | `appsettings.json` の場所 |
| --- | --- |
| インストーラ版 | `%LOCALAPPDATA%\Programs\FileHistoryClone\appsettings.json` |
| portable zip 版 | `FileHistory.exe` と同じフォルダ |

すべてのパスで環境変数展開(例: `%USERPROFILE%\Documents`)が使えます。

| キー | 説明 |
| --- | --- |
| `BackupBaseDir` | バックアップ先のルート。実データは `{BackupBaseDir}\{ユーザー名}\{マシン名}\Data` 配下に保存 |
| `DefaultBackupInterval` | 同一ファイルを再バックアップするまでの最短秒数 |
| `IncludeDirs` | 保護対象フォルダ。エントリごとに `BackupInterval` を上書き可能(最長一致優先) |
| `ExcludeDirs` | 除外パターン。絶対パス、任意の深さで一致する名前(`.git`)、glob(`*.tmp`)、除外の例外(`!important.log`)、`#` で始まるコメント行に対応 |
| `CrawlingInterval` | フルクロール完了後、次回クロールまでの待機秒数 |
| `CrawlingIdleTimer` | クロールを開始するのに必要なユーザー無操作時間(秒) |
| `Language` | UI 言語(`"ja"`、`"en"`)。空なら OS に追従 |
| `MaxGenerations` | 1ファイルあたりの最大保持世代数(0 = 無制限) |
| `RetentionDays` | この日数より古いバックアップを削除。最新世代は常に保持(0 = 無制限) |
| `RetentionScanInterval` | 保持ポリシー適用スキャンの実行間隔(秒) |

コメント付きのテンプレートは [appsettings.example.json](FileHistory/appsettings.example.json) を参照してください。

## 動作の仕組み

```
┌──────────────────┐   変更イベント     ┌─────────────────┐  コピータスク   ┌────────────┐
│ DirectoryWatcher ├──────────────────►│                 ├───────────────►│ CopyWorker │
└──────────────────┘  (高プライオリティ) │ BackupScheduler │                └─────┬──────┘
┌──────────────────┐                   │ (時刻順キュー)   │                      ▼
│ Crawler          ├──────────────────►│                 │              バックアップファイル
└────────┬─────────┘  (低プライオリティ) └─────────────────┘              + LiteDB カタログ
         │ アイドル時のみ実行
┌────────┴─────────┐
│ IdleTimeWatcher  │
└──────────────────┘
```

- **DirectoryWatcher** がファイルシステムイベントを検知して高優先度のバックアップ要求を登録します。
- **Crawler** が最低優先度スレッドで対象ディレクトリを巡回し、未バックアップのファイルを登録します。
- **BackupScheduler** はファイルがバックアップ間隔の間更新されていないことを確認してから、最大10並列でコピーします。失敗したコピーはロールバックされます。
- **RetentionWorker** が保持ポリシーを定期適用します。
- カタログ(`Catalog.db`)がディレクトリ・ファイル・世代・タイムスタンプの対応を管理します。

## リポジトリ構成

| プロジェクト | 説明 |
| --- | --- |
| `FileHistory` | 本体(タスクトレイ常駐アプリ) |
| `FileHistoryTests` | MSTest によるユニット/結合テスト |

## 類似プロジェクト

FileHistoryClone が要件に合わない場合は、以下のオープンソース代替も検討してください:

| プロジェクト | 方式 | FileHistoryClone との違い |
| --- | --- | --- |
| [Home Backup & Restore](https://github.com/osmanonurkoc/home_backup_restore) | NTFS ハードリンクによる Time Machine 風スナップショット (PowerShell + WPF) | 手動/スケジュール実行のスナップショット型。FileHistoryClone は変更を検知して継続的にバックアップ |
| [Kopia](https://github.com/kopia/kopia) | スケジュールスナップショット + 重複排除・圧縮・暗号化 | 高機能だが独自リポジトリ形式。ファイル単位のプレーンコピーではない |
| [Restic](https://github.com/restic/restic) | CLI のスナップショットバックアップ(暗号化リポジトリ) | CLI 中心・スナップショット型・独自リポジトリ形式 |
| [Duplicati](https://github.com/duplicati/duplicati) | ローカル/クラウドへのスケジュールバックアップ | スケジュール型で、データをブロック形式でアーカイブ(エクスプローラーで直接閲覧不可) |

FileHistoryClone の立ち位置は、**イベント駆動の継続的なファイル単位世代管理を、エクスプローラーでそのまま開ける通常ファイルとして保存する**ことです。本家 Windows「ファイル履歴」に最も近い思想のツールを目指しています。

## 既知の制限

- インクルード対象はローカルドライブのみ(UNC パス `\\server\share` は未対応)。
- Volume Shadow Copy 非対応のため、他プロセスが排他ロック中のファイルはその間バックアップできません。
- バックアップは無圧縮・無暗号化(単純コピー)です。
- Windows 専用です(WinForms + Win32 アイドル検知)。

## コントリビュート

バグ報告・機能要望・プルリクエストを歓迎します。翻訳の追加(`Strings.<言語>.resx` を1ファイル追加するだけ、コード変更不要)やドキュメント修正も大歓迎です。セットアップ手順とガイドラインは [CONTRIBUTING.md](CONTRIBUTING.md)、リリース履歴は [CHANGELOG.md](CHANGELOG.md) を参照してください。

このツールがあなたのファイルを救ったら、ぜひリポジトリに ⭐ をお願いします。他の人がこのツールを見つける助けになります。

## ライセンス

[MIT](LICENSE)
