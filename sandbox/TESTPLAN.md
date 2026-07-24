# FileHistoryClone v1.0.1 候補版 — Windows Sandbox 試験手順

準備は自動で完了しています:
- アプリは `C:\FileHistoryClone` に展開・起動済み(タスクトレイに常駐)
- `Documents` に試験用ファイルを作成済み: `sample.txt`、`SubFolder\nested.txt`、`ignore-me.tmp`
- 既定の保護対象は `Documents`、バックアップ先は `%USERPROFILE%\FileHistoryCloneBackup`

Sandbox を閉じればすべて破棄されます。ホスト側には一切影響しません。

> **補助スクリプト**: Sandbox にはメモ帳が無く、クリップボード共有も効かないことがあります。
> ファイル編集などの操作は同梱の `tools.ps1` で行えます。PowerShell を開いて:
>
> ```
> powershell -ep bypass -File C:\sandbox\tools.ps1
> ```
>
> と打つとコマンド一覧が出ます(以下の手順中でも都度案内します)。

## 1. 初回起動

- [ ] タスクトレイに FileHistoryClone のアイコンが表示される
- [ ] 初回通知(バックアップ開始/保護対象)が表示される
- [ ] トレイの右クリックメニューに「設定を開く」が **ない** こと(メイン画面へ移動した)

## 2. メイン画面(トレイアイコン → 開く)

- [ ] タイトルバーに「FileHistoryClone ファイル復元 - v1.0.1」とバージョンが表示される
- [ ] メニューバー「ツール」→「設定...」で設定画面が開く
- [ ] 「ツール」→「設定ファイルの場所を開く」でエクスプローラーが appsettings.json を選択した状態で開く
- [ ] 「ヘルプ」→「バージョン情報...」で製品名・バージョン・設定ファイルパスが表示される

## 3. 設定画面(今回の修正の重点)

- [ ] スクロールバーが出ず、最下部の「保存」「キャンセル」まで一画面に収まっている
- [ ] タイトルバー左上がアプリ独自のアイコンになっている
- [ ] 「バックアップの保存先」「保護対象のフォルダ」に「フォルダ:」ラベルがなく、入力欄が左端まで広がっている
- [ ] 保護対象フォルダ欄で各行を直接編集できる。最下部に常に空行があり、そこに直接パスを入力できる
- [ ] 保護対象フォルダ欄・除外パターン欄で Enter キーを押すと**改行が入る**(保存が実行されない)
- [ ] 「同一ファイルの再バックアップ最短間隔」の右の「個別設定...」でフォルダごとの間隔を設定できる
      (空欄 = 既定値。設定して保存後、appsettings.json の IncludeDirs に BackupInterval が書かれる)
- [ ] 「保持ポリシー適用の実行間隔」の項目が **なくなっている**
- [ ] 「フルクロール間の待機時間(秒)」という表記になっている
- [ ] 各項目にマウスを載せるとツールチップ(説明+既定値)が表示される

### 3a. 640x480 での表示確認

Sandbox 内の PowerShell で(設定アプリにディスプレイ設定が無い環境向け):

```
powershell -ep bypass -File C:\sandbox\tools.ps1 res640
```

(戻り値 0 なら成功。Sandbox ウィンドウを小さくリサイズして代用してもよい)

- [ ] 設定画面が画面内に収まり、スクロールバーが表示される
- [ ] **最下部(UI 言語)まで**スクロールして表示・操作できる
- [ ] 「保存」「キャンセル」ボタンはスクロールに関係なく常に見えている
- [ ] 確認後、解像度を元に戻す: `powershell -ep bypass -File C:\sandbox\tools.ps1 res1280`

## 4. バックアップ動作

確認しやすくするため、設定画面で以下に変更 → 保存 → 再起動プロンプトで再起動:
- 「同一ファイルの再バックアップ最短間隔」 → `10`
- 「クロール開始までのアイドル時間」 → `0`(常時クロール)

- [ ] `powershell -ep bypass -File C:\sandbox\tools.ps1 edit-sample` で `Documents\sample.txt` を編集すると、
      しばらくして `%USERPROFILE%\FileHistoryCloneBackup\<ユーザー>\<マシン>\Data` 配下にバックアップが作られる
      (確認: `powershell -ep bypass -File C:\sandbox\tools.ps1 show-backup`)
- [ ] `ignore-me.tmp` はバックアップされない(`*.tmp` 除外パターンの確認)
- [ ] `SubFolder\nested.txt` もバックアップされる(サブフォルダの確認)

## 5. 復元

- [ ] メイン画面のツリーに sample.txt が表示される
- [ ] 過去の版を「一時コピーして開く」で内容確認できる
- [ ] 復元を実行すると元の場所に書き戻される(上書き確認が出る)

## 6. 設定の保存(appsettings.json の互換性)

- [ ] 保存後、`C:\FileHistoryClone\appsettings.json` に変更が反映され、
      `Logging` セクションと `RetentionScanInterval` が消えずに残っている
- [ ] わざと不正な状態(バックアップ先を空にする等)で保存すると警告が出て保存されない

## 7. 言語切替

- [ ] UI 言語を `English` にして保存・再起動 → 画面・メニュー・説明文・ツールチップが英語になる

## 8. 保持ポリシー

- [ ] 「1ファイルの最大世代数」を `1` にして保存。続いて
      `powershell -ep bypass -File C:\sandbox\tools.ps1 retention60` で `RetentionScanInterval` を `60` に変更 → 再起動
- [ ] `powershell -ep bypass -File C:\sandbox\tools.ps1 edit-sample3` で sample.txt を 12 秒間隔で 3 回編集
      → **起動 5 分後以降**に古い世代が削除され最新のみ残る(確認: `tools.ps1 show-backup`)
      (保持ワーカーは起動直後 5 分待ってから動き始める仕様)

## 9. バックアップ整理画面

- [ ] メイン画面「バックアップ整理」を開くと、下部に
      「この整理は一時的なものです。…設定画面の保持ポリシーを設定してください」の注意書きが表示される

## 10. v1.0.0 からのアップグレード試験

`C:\sandbox` に両バージョンのインストーラを用意してあります。
**先に、自動起動している portable 版をトレイ →「終了」で止めてから**実施してください(多重起動防止のため)。

1. `C:\sandbox\FileHistoryCloneSetup-1.0.0.exe` を実行して v1.0.0 をインストール・起動
2. 何かファイルがバックアップされるまで待つ(= Catalog.db が作られる)
   - 確認: `%USERPROFILE%\FileHistoryCloneBackup\<ユーザー>\<マシン>\Configuration\Catalog.db` が存在
3. `powershell -ep bypass -File C:\sandbox\tools.ps1 marker` で
   `%LOCALAPPDATA%\Programs\FileHistoryClone\appsettings.json` の ExcludeDirs に `"my-marker"` を追加(引き継ぎ確認用)して、トレイ →「終了」
4. `C:\sandbox\FileHistoryCloneSetup-1.0.1.exe` を実行して上書きインストール

- [ ] インストール後の起動で**初回セットアップ(設定画面)が出ない**(v1.0.0 利用者はスキップされる)
- [ ] メイン画面タイトルが v1.0.1 になっている
- [ ] appsettings.json が上書きされておらず、手で追加した `my-marker` とコメントが残っている
- [ ] 設定画面を開くと ExcludeDirs に `my-marker` が表示される
- [ ] v1.0.0 時代のバックアップがメイン画面のツリーに表示され、復元できる
- [ ] 参考: 設定画面で保存すると JSON のコメントは消える(仕様。値と Logging セクションは保持される)

## 試験後

Sandbox のウィンドウを閉じるだけで環境ごと破棄されます。

---

### コードを修正して再試験するとき(ホスト側で実行)

```
dotnet publish FileHistory/FileHistory.csproj -c Release -r win-x64 --self-contained true -o sandbox/publish
```

を実行してから `sandbox\FileHistoryClone-Test.wsb` をダブルクリック。
