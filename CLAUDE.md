# FileHistoryClone

Windows ファイル履歴風の継続バックアップツール(.NET 8 / WinForms)。

## Windows Sandbox での手動試験

リリース前の手動試験は Windows Sandbox で行う。

- **起動方法**: `C:\Users\tomoy\source\repos\FileHistoryClone\sandbox\FileHistoryClone-Test.wsb` をダブルクリック
- 試験手順書: `sandbox\TESTPLAN.md`(Sandbox 内では Edge で自動表示される)
- ビルド更新: `dotnet publish FileHistory/FileHistory.csproj -c Release -r win-x64 --self-contained true -o sandbox/publish` を実行してから .wsb を開き直す
- `sandbox/publish/` は .gitignore 済み(生成物)

**Sandbox 試験環境について回答するときは、必ず .wsb ファイルのフルパスを明示すること。**

## ビルド・テスト

- ビルド: `dotnet build FileHistory/FileHistory.csproj`
- テスト: `dotnet test FileHistoryTests/FileHistoryTests.csproj`

## UI 変更の検証

UI を変更したら、`tools/FormPreview` ハーネスでスクリーンショットを撮り、Read で目視確認してから完了報告する(ja/en 両方)。

```
dotnet run --project tools/FormPreview -- ja-JP ja
dotnet run --project tools/FormPreview -- en-US en
```

全フォーム(設定・640x480シミュレーション・メイン・個別設定・整理)を PNG 出力する。詳細は Program.cs 冒頭コメント。

## リリース

手順は `.claude/skills/release/SKILL.md`(publish → zip → Inno Setup → GitHub Release → winget)。リリースは github リモート側。

## 補足

- appsettings.json は exe と同じフォルダに読み書きされる
- 文字列リソースは Strings.resx(英語)/ Strings.ja.resx(日本語)の両方を常に更新する
- リモートは origin(Azure DevOps / master)と github(GitHub / main)。リリースは github 側
