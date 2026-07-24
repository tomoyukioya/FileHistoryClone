# FileHistoryClone v1.1.0 候補版 — Windows Sandbox 試験手順

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

- [ ] タイトルバーに「FileHistoryClone ファイル復元 - v1.1.0」とバージョンが表示される
- [ ] メイン画面に「バックアップ整理」**ボタンが無い**こと(ツールメニューへ移動した)
- [ ] メニューバー「ツール」に「設定...」「設定ファイルの場所を開く」「バックアップ整理...」が並んでいる
- [ ] 「ツール」→「設定...」で設定画面が開く
- [ ] 「ツール」→「設定ファイルの場所を開く」でエクスプローラーが appsettings.json を選択した状態で開く
- [ ] 「ヘルプ」→「バージョン情報...」で製品名・バージョン・設定ファイルパスが表示される

## 3. 設定画面

- [ ] スクロールバーが出ず、最下部の「保存」「キャンセル」まで一画面に収まっている
- [ ] タイミングのセクションに「同一ファイルの再バックアップ最短間隔」だけがあり、
      「クロール開始までのアイドル時間」「フルクロール間の待機時間」の項目が **なくなっている**
- [ ] 「保持ポリシー適用の実行間隔」の項目も引き続き **ない**
- [ ] 保護対象フォルダ欄・除外パターン欄の直接編集、Enter で改行、が引き続き動く
- [ ] 「個別設定...」でフォルダごとの間隔を設定できる
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

確認しやすくするため:
1. 設定画面で「同一ファイルの再バックアップ最短間隔」→ `10` にして保存(再起動はまだしない)
2. `powershell -ep bypass -File C:\sandbox\tools.ps1 crawlfast` で CrawlingIdleTimer を 0 に変更
   (UI から消えた項目が JSON 直編集で変えられることの確認も兼ねる)
3. 再起動(トレイ → 終了 → `C:\FileHistoryClone\FileHistory.exe`)

- [ ] `powershell -ep bypass -File C:\sandbox\tools.ps1 edit-sample` で `Documents\sample.txt` を編集すると、
      しばらくして `%USERPROFILE%\FileHistoryCloneBackup\<ユーザー>\<マシン>\BackupFiles` 配下にバックアップが作られる
      (確認: `powershell -ep bypass -File C:\sandbox\tools.ps1 show-backup`)
- [ ] バックアップ先のフォルダ名が `BackupFiles` と `Database` である(旧 `Data`/`Configuration` が **ない**)
- [ ] `ignore-me.tmp` はバックアップされない(`*.tmp` 除外がクロール経由でも効くことの確認)
- [ ] `SubFolder\nested.txt` もバックアップされる(サブフォルダの確認)
- [ ] 設定保存後の appsettings.json で `Logging` セクションと `CrawlingIdleTimer` の値(0)が消えていない
      (確認: `tools.ps1 show-config`)

## 5. 復元

- [ ] メイン画面のツリー(ルートが `BackupFiles` 表記)に sample.txt が表示される
- [ ] 過去の版を「一時コピーして開く」で内容確認できる
- [ ] 復元を選ぶと、**元ファイルのあったフォルダ(Documents)が初期表示**される
- [ ] 復元を実行すると元の場所に書き戻される(上書き確認が出る)

## 6. 保持ポリシー(保存時の即時適用)

1. 設定画面で「1ファイルの最大世代数」→ `1` にして保存 → 再起動

- [ ] `powershell -ep bypass -File C:\sandbox\tools.ps1 edit-sample3`(12 秒間隔で 3 回編集)を実行し、
      バックアップが進むたびに `show-backup` で確認すると、**スキャンを待たずに** sample.txt の世代が常に 1 つに保たれる
      (保存時プルーニングの確認。旧版はスキャン実行まで余分な世代が残っていた)

## 7. 言語切替

- [ ] 設定画面(アプリの「ツール」→「設定...」)で UI 言語を `English` にして保存・再起動
      → 画面・メニュー・説明文・ツールチップが英語になる(Windows の言語設定は使わない)

## 8. バックアップ整理画面

- [ ] メイン画面「ツール」→「バックアップ整理...」で開き、下部に
      「この整理は一時的なものです。…設定画面の保持ポリシーを設定してください」の注意書きが表示される

## 9. v1.0.0 からのアップグレード試験(フォルダ名移行を含む)

`C:\sandbox` に両バージョンのインストーラを用意してあります。
**先に、自動起動している portable 版をトレイ →「終了」で止めてから**実施してください(多重起動防止のため)。
また、portable 版と区別するため、この試験の前に `%USERPROFILE%\FileHistoryCloneBackup` を削除しておくときれいに確認できます:
`powershell -ep bypass -File C:\sandbox\tools.ps1 clean-backup`

1. `powershell -ep bypass -File C:\sandbox\tools.ps1 install100` で v1.0.0 をインストール・起動
   (「インストール準備中」で固まったら別 PowerShell で `tools.ps1 unstick` — v1.0.0 の taskkill が WMI 停止で固まる既知問題)
2. `tools.ps1 edit-sample` で編集し、バックアップが作られるまで待つ
   - 確認: `tools.ps1 show-backup` で `...\Configuration\Catalog.db` と `...\Data\...` が存在(**旧フォルダ名**であること)
3. `powershell -ep bypass -File C:\sandbox\tools.ps1 marker` で ExcludeDirs に `"my-marker"` を追加(引き継ぎ確認用)して、トレイ →「終了」
4. `powershell -ep bypass -File C:\sandbox\tools.ps1 install101` で v1.1.0 を上書きインストール
   (今回のインストーラは WMI 停止環境でも固まらないはず — 固まらないこと自体も確認項目)

- [ ] v1.1.0 のインストールが「インストール準備中」で止まらず完了する
- [ ] インストール後の起動で**初回セットアップ(設定画面)が出ない**(既存利用者はスキップされる)
- [ ] メイン画面タイトルが v1.1.0 になっている
- [ ] `tools.ps1 show-backup` で **`Database` と `BackupFiles` にリネームされ、旧 `Configuration`/`Data` が消えている**
- [ ] appsettings.json が上書きされておらず、手で追加した `my-marker` とコメントが残っている(`tools.ps1 show-configi`)
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
