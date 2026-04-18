# grzyClothTool 日本語版 使い方ガイド

> [grzybeek/grzyClothTool](https://github.com/grzybeek/grzyClothTool) の
> 日本語ローカライズフォークです。本書はフォーク固有の設定とツールの
> 基本的な使い方をまとめたものです。上流のリポジトリ README も併せて
> 参照してください。

---

## 目次

1. [動作環境](#動作環境)
2. [入手と起動](#入手と起動)
3. [言語切り替え](#言語切り替え)
4. [基本ワークフロー](#基本ワークフロー)
5. [UI の構成](#ui-の構成)
6. [ビルド (addon 書き出し)](#ビルド-addon-書き出し)
7. [3D プレビュー](#3d-プレビュー)
8. [重複検査と一括処理](#重複検査と一括処理)
9. [プロジェクトの保存とオートセーブ](#プロジェクトの保存とオートセーブ)
10. [設定とログ](#設定とログ)
11. [トラブルシューティング](#トラブルシューティング)
12. [既知の問題 (上流から継承)](#既知の問題-上流から継承)
13. [開発者向け: 翻訳の追加・修正](#開発者向け-翻訳の追加修正)
14. [ライセンス](#ライセンス)

---

## 動作環境

- **OS**: Windows 10 / 11 (x64)
- **ランタイム**: .NET 10 (self-contained ビルドを使う場合は不要)
- **必要 DLL**: `CodeWalker.dll`, `CodeWalker.Core.dll` (同梱)
- **GPU**: 3D プレビューを使う場合は DirectX 11 対応 GPU
- **GTA5 インストール**: 3D プレビューで ped モデルを読み込む場合のみ
  必要。addon 作成のみであれば未インストールでも動作します。

## 入手と起動

### 方法 A: Release zip を使う (推奨)

1. [Releases ページ](https://github.com/Laplash1/grzyClothTool/releases)
   から `grzyClothTool-ja-vX.Y.Z-win-x64.zip` を取得します。
2. 任意のフォルダに展開します (Program Files 直下は推奨しません、
   書き込み権限の都合)。
3. `grzyClothTool.exe` をダブルクリックで起動します。
4. 初回起動時に **Windows SmartScreen** が
   「Windows によって PC が保護されました」と表示する場合があります。
   コード署名が未付与のためです。「詳細情報」→「実行」で起動できます。

### 方法 B: ソースからビルドする

1. `git clone https://github.com/Laplash1/grzyClothTool.git`
2. `cd grzyClothTool`
3. `dotnet publish grzyClothTool/grzyClothTool.csproj -c Release -r win-x64 --self-contained`
4. `grzyClothTool/bin/Release/net10.0-windows/win-x64/publish/grzyClothTool.exe`
   を起動

初回起動時に **初期設定ウィザード** が開きます。GTA5 のインストール
フォルダ (任意) と作業フォルダを指定してください。

> :warning: **ドライブ直下 (例: `C:\`) を作業フォルダに指定しないで
> ください。** 誤って大量のファイルを削除する事故につながります。
> フォーク版では直下指定時に警告ダイアログを出します。

## 言語切り替え

本フォークでは日本語 / 英語の 2 言語を同梱しています。

### 起動時の判定順

1. 環境変数 `GRZY_CLOTHTOOL_LANG` (`ja` / `en`)
2. 設定ファイル (`Settings.Default.Language`)
3. 既定値 **`ja`** (日本語)

### 英語で起動したい場合

コマンドプロンプト:

```bat
set GRZY_CLOTHTOOL_LANG=en
grzyClothTool.exe
```

PowerShell:

```powershell
$env:GRZY_CLOTHTOOL_LANG = "en"
.\grzyClothTool.exe
```

恒久的に切り替えたい場合は「設定」ウィンドウから **言語** を変更し、
アプリを再起動してください (実行中の動的切替は今後対応予定)。

## 基本ワークフロー

典型的な作業の流れです。

1. **プロジェクトを新規作成** (メインウィンドウ → 新規)
2. **Addon を追加** (プロジェクトウィンドウ左上) — 1 つの addon に
   ドロワブルを詰めていきます。128 個を超えると自動で分割されます。
3. **Drawable (ydd/ytd) をドラッグ & ドロップで追加**
   - フォルダ全体をドロップすると一括読み込みが可能
   - male / female の識別が必要な場合はダイアログが表示されます
4. **Drawable を編集** — 右側のペインで以下を設定
   - コンポーネントタイプ (shirt / pants / hair など)
   - 性別 (男性 / 女性)
   - フラグ (BERD, Mask, hair-shrink など)
   - テクスチャのバリエーション
5. **3D プレビューで確認** (左下の Preview タブ)
6. **重複検査** (Duplicate Inspector) で同一ドロワブルの整理
7. **ビルド** — FiveM / AltV / シングルプレイヤー向けに書き出し
8. **保存** (メニュー → 保存 / `Ctrl+S`) — 作業プロジェクト (`.grzy`)
   として保存、後で再開可能

## UI の構成

| 画面 | 役割 |
|------|------|
| メインウィンドウ | プロジェクト全体のハブ (メニュー、タブ、ログ) |
| Home | ツール概要、最近のプロジェクト、アナウンス |
| プロジェクトウィンドウ | Addon とドロワブル一覧、並び替え、移動 |
| Drawable 詳細 | 選択中ドロワブルのプロパティとテクスチャ |
| Build ウィンドウ | 書き出し先とターゲット (FiveM/AltV/SP) |
| Optimize ウィンドウ | テクスチャ圧縮 (DXT1/3/5) の一括処理 |
| Duplicate Inspector | 重複ドロワブルの検出と統合 |
| 設定ウィンドウ | 言語、テーマ、フォルダ、外部ツール連携 |
| ログウィンドウ | 直近の操作履歴 (エラー時の確認用) |

## ビルド (addon 書き出し)

1. Build ウィンドウを開く (メニュー → Build)
2. **出力フォルダ** を指定
3. **ターゲット** を選択: FiveM / AltV / Singleplayer
4. **検証** に失敗した項目があれば一覧が表示されます
5. **ビルド開始** — 進捗バーと現在処理中のドロワブル名が表示されます
6. 完了後、出力フォルダの `stream/` に ydd/ytd/ymt/meta 一式が並びます

> :information_source: **テンプファイルの掃除:** ビルド時は作業用の
> 一時フォルダを作成します。失敗/キャンセル時は自動で掃除を試みますが、
> 完全に削除できない場合は警告ログが出力されます。

## 3D プレビュー

- プレビューには CodeWalker の 3D エンジンを利用しています。
- GTA5 が未インストールの環境では「GTA が見つかりません」プレースホルダ
  が表示されます。
- GPU 初期化に失敗した場合はソフトウェアフォールバックに切替わります。
- 髪の縮み / ヒール高さの確認など、編集中に数値を追い込む用途を想定
  しています。

## 重複検査と一括処理

Duplicate Inspector は同一ファイルハッシュを持つドロワブルを検出します。

- **検出** — 「スキャン開始」で全 addon を走査
- **グループ単位の削除** — 代表 1 件を残して他を削除
- **一括置換** — 別ファイルで一括置き換え
- スキャン終了時に「N 件中 M グループ検出」のサマリを表示

## プロジェクトの保存とオートセーブ

- プロジェクトは `.grzy` (JSON) として保存されます。
- **オートセーブ** は数分おきに `autosave.json` / `autosave.external.json`
  に書き出します。クラッシュ時は次回起動時に復元を提案します。
- ステータスバーの自動保存インジケータで状態を確認できます。

## 設定とログ

- **設定ウィンドウ** → 言語、テーマ、作業フォルダ、外部エディタ、
  テクスチャ圧縮の既定値などを変更可能。
- **ログウィンドウ** → 進捗メッセージ、警告、エラーが時系列で並びます。
- **エラーログファイル** → 例外発生時は実行フォルダ下に日付付きの
  ログファイルが保存されます (Sentry への匿名レポート送信は設定で
  無効化できます)。

## トラブルシューティング

| 症状 | 対処 |
|------|------|
| 起動直後に落ちる | 上流 Issue #50 を参照。GPU ドライバ更新で改善する事例あり |
| 「!Str.Foo.Bar!」と画面に表示される | 翻訳辞書のキー欠落。Issue で報告してください |
| BERD/Mask のチェックが保存されない | 上流 Issue #55 (未解決) |
| 3D プレビューが真っ黒 | GTA5 のインストールパス設定を見直し、GPU ドライバを更新 |
| ビルド時に RPF エラー | 上流 Issue #49 を参照 (特定 RPF で再現) |
| 英語表記のままの項目がある | フォーク開発中。Issue で該当画面を教えてください |

## 既知の問題 (上流から継承)

本フォーク初版では以下の上流 Issue を継承しています (詳細は
[NOTICE](NOTICE) 参照):

- `grzybeek/grzyClothTool#55` BERD/Mask チェックの非永続化
- `grzybeek/grzyClothTool#50` 起動時クラッシュ (部分緩和済)
- `grzybeek/grzyClothTool#49` RPF_INVALID_ENTRY_4
- `grzybeek/grzyClothTool#56` 3D Preview の drawable 更新失敗
- `grzybeek/grzyClothTool#46` 男性 ped で女性の透明 drawable が表示

## 開発者向け: 翻訳の追加・修正

### 辞書ファイル

- `grzyClothTool/Resources/Strings/Strings.en.xaml`
- `grzyClothTool/Resources/Strings/Strings.ja.xaml`

両ファイルは `ResourceDictionary` で、`x:Key="Str.<領域>.<用途>"`
形式のキーを持ちます。EN/JA でキー数とプレースホルダ `{0}` の個数を
一致させてください。

### XAML からの参照

```xaml
<TextBlock Text="{DynamicResource Str.Home.Welcome}" />
```

`DynamicResource` を使用することで、将来の動的言語切替に備えています。

### C# からの参照

```csharp
using grzyClothTool.Helpers;

var title = LocalizationHelper.Get("Str.Common.Error");
var msg   = LocalizationHelper.GetFormat("Str.Build.FailedFormat", fileName);
```

- `Get(key)` — キーが見つからない場合は `!key!` を返します (可視化目的)。
- `GetFormat(key, args...)` — `string.Format` 例外は黙殺し fmt を返します。

### 命名規則

- `Str.Common.*` — 複数画面で共有 (OK, Cancel, Error など)
- `Str.<画面名>.Dialog.*` — MessageBox / CustomMessageBox のタイトル・本文
- `Str.<画面名>.Log.*` — LogHelper / ProgressHelper 経由の進捗メッセージ
- `Str.<画面名>.Filter.*` — OpenFileDialog / SaveFileDialog の拡張子フィルタ
- `*.Format` で終わるキー — `{0}` を含む書式文字列

### 翻訳を増やす

1. 該当 XAML / C# の文字列を `{DynamicResource}` または
   `LocalizationHelper.Get(…)` に置換
2. `Strings.en.xaml` と `Strings.ja.xaml` の両方に同じキーを追加
3. `{0}` の数が両言語で一致することを確認

## ライセンス

- 本フォーク: GNU General Public License v3.0 (上流を継承)
- 詳細: [LICENSE](LICENSE) / [NOTICE](NOTICE)
- 上流: <https://github.com/grzybeek/grzyClothTool>
- CodeWalker: © dexyfex <https://github.com/dexyfex/CodeWalker>
