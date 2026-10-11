# Bait Beacon

Roxyz0501 による独立した Dalamud プラグインです。GatherBuddy Reborn に保存された Auto-Gather リストを選び、リスト内の魚に必要な餌を表示します。所持品、チョコボかばん、リテイナーの対応画面では該当する餌の枠を強調表示します。

## 使い方

ゲーム内で `/baitbeacon` を実行し、保存済みリストを選択します。Reborn の `auto_gather_lists.json` は読み取り専用です。Config で GatherBuddy Reborn と GatherBuddy の導入・読み込み状態を確認できます。

元の GatherBuddy には互換 Auto-Gather リストがないため、リストの読み込み対象は Reborn のみです。餌データのない魚は一覧で分けて表示します。

魚と餌の対応表は 2026-10-11 時点の上流データです。Reborn 側で利用者が追加した魚データの上書きは読み込みません。

## 言語

Config で日本語、英語、ドイツ語、フランス語、韓国語、簡体字中国語、繁体字中国語を切り替えられます。初回だけゲーム言語、対応する Dalamud UI 言語、英語の順に決定して保存します。アイテム名はゲームが提供する言語データを使用し、韓国語・中国語では英語名になる場合があります。

## 開発状況

Release ビルドと保存リストの解析テストは完了しています。ゲーム内での枠位置とフォント表示は未確認です。公開版はまだありません。

## 参照元・ライセンス

保存リスト形式と魚・餌データは The Combat Reborn Team の [GatherBuddy Reborn](https://github.com/FFXIV-CombatReborn/GatherBuddyReborn)（Apache-2.0）を参照しました。元の [GatherBuddy](https://github.com/Ottermandias/GatherBuddy) は Ottermandias ほかによる Apache-2.0 のプラグインです。所持品画面のアドオン名・枠順は Tischel の [Inventory Search Bar](https://github.com/Tischel/InventorySearchBar)（AGPL-3.0）とも照合しました。これらのプラグインの実装コードは含みません。生成した餌データには同梱の Apache-2.0 ライセンスと NOTICE を適用します。

任意の支援先: [Roxyz0501 の Ko-fi](https://ko-fi.com/roxyz0501)
