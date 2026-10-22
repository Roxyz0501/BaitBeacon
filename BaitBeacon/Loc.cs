using Dalamud.Game;

namespace BaitBeacon;

public static class Loc
{
    public static readonly (string Code, string Native)[] Languages =
    [
        ("ja", "日本語"), ("en", "English"), ("de", "Deutsch"), ("fr", "Français"),
        ("ko", "한국어"), ("zh-Hans", "简体中文"), ("zh-Hant", "繁體中文")
    ];

    private static readonly Dictionary<string, string[]> Text = new()
    {
        ["Title"] = ["餌の目印", "Bait Beacon", "Köderanzeige", "Repère d’appâts", "미끼 표시", "鱼饵标记", "魚餌標記"],
        ["CommandHelp"] = ["Bait Beacon を開きます。", "Open Bait Beacon.", "Bait Beacon öffnen.", "Ouvrir Bait Beacon.", "Bait Beacon을 엽니다.", "打开 Bait Beacon。", "開啟 Bait Beacon。"],
        ["Lists"] = ["リスト", "Lists", "Listen", "Listes", "목록", "列表", "清單"],
        ["Settings"] = ["設定", "Settings", "Einstellungen", "Paramètres", "설정", "设置", "設定"],
        ["LanguageSettings"] = ["言語設定", "Language settings", "Spracheinstellungen", "Paramètres de langue", "언어 설정", "语言设置", "語言設定"],
        ["Support"] = ["支援", "Support", "Unterstützen", "Soutien", "후원", "支持", "支援"],
        ["Language"] = ["言語 / Language", "Language / 言語", "Sprache / Language", "Langue / Language", "언어 / Language", "语言 / Language", "語言 / Language"],
        ["LanguageHelp"] = ["表示言語を選択します。変更はすぐに反映され、次回起動後も保持されます。", "Choose the display language. Changes apply immediately and remain after restarting.", "Wähle die Anzeigesprache. Die Änderung gilt sofort und bleibt nach dem Neustart erhalten.", "Choisissez la langue d’affichage. Le changement s’applique aussitôt et reste enregistré après le redémarrage.", "표시 언어를 선택하세요. 변경 사항은 즉시 적용되며 다시 시작해도 유지됩니다.", "选择显示语言。更改会立即生效，并在重新启动后保留。", "選擇顯示語言。變更會立即生效，重新啟動後仍會保留。"],
        ["Dependency"] = ["前提プラグイン", "Companion plugins", "Begleit-Plugins", "Plugins associés", "연동 플러그인", "关联插件", "關聯外掛"],
        ["Installed"] = ["導入済み", "Installed", "Installiert", "Installé", "설치됨", "已安装", "已安裝"],
        ["Loaded"] = ["読み込み中", "Loaded", "Geladen", "Chargé", "로드됨", "已加载", "已載入"],
        ["Missing"] = ["未導入", "Not installed", "Nicht installiert", "Non installé", "미설치", "未安装", "未安裝"],
        ["Original"] = ["GatherBuddy（元版）には互換 Auto-Gather リストがありません。", "Original GatherBuddy has no compatible Auto-Gather list.", "Das ursprüngliche GatherBuddy hat keine kompatible Auto-Gather-Liste.", "Le GatherBuddy d’origine n’a pas de liste Auto-Gather compatible.", "원본 GatherBuddy에는 호환되는 Auto-Gather 목록이 없습니다.", "原版 GatherBuddy 没有兼容的 Auto-Gather 列表。", "原版 GatherBuddy 沒有相容的 Auto-Gather 清單。"],
        ["Select"] = ["保存済みリスト", "Saved list", "Gespeicherte Liste", "Liste enregistrée", "저장된 목록", "已保存列表", "已儲存清單"],
        ["Reload"] = ["再読み込み", "Reload", "Neu laden", "Recharger", "다시 불러오기", "重新加载", "重新載入"],
        ["Retry"] = ["再試行", "Retry", "Erneut versuchen", "Réessayer", "다시 시도", "重试", "重試"],
        ["ListStopped"] = ["読込が3回連続で失敗したため停止しました。リストの「再読み込み」で再開できます。", "List reading stopped after three failures. Use Reload on Lists to resume.", "Lesen nach drei Fehlern gestoppt. Mit Neu laden unter Listen fortsetzen.", "Lecture arrêtée après trois échecs. Utilisez Recharger dans Listes.", "3회 연속 실패로 읽기를 중지했습니다. 목록에서 다시 불러오기를 누르세요.", "连续三次失败，已停止读取。在列表中点击重新加载以恢复。", "連續三次失敗，已停止讀取。在清單中點選重新載入以恢復。"],
        ["OverlayStopped"] = ["強調表示が3回連続で失敗したため停止しました。原因を解消して再試行してください。", "Highlighting stopped after three failures. Resolve the cause, then retry.", "Hervorhebung nach drei Fehlern gestoppt. Ursache beheben und erneut versuchen.", "Surlignage arrêté après trois échecs. Corrigez la cause puis réessayez.", "3회 연속 실패로 강조 표시를 중지했습니다. 원인을 해결한 뒤 다시 시도하세요.", "连续三次失败，已停止高亮。解决原因后重试。", "連續三次失敗，已停止醒目提示。解決原因後重試。"],
        ["SaveStopped"] = ["設定保存が3回連続で失敗したため自動保存を停止しました。変更は未保存です。原因を解消して再試行してください。", "Autosave stopped after three failures. Changes remain unsaved. Resolve the cause, then retry.", "Automatisches Speichern nach drei Fehlern gestoppt. Änderungen sind ungespeichert. Ursache beheben und erneut versuchen.", "Sauvegarde automatique arrêtée après trois échecs. Modifications non enregistrées. Corrigez la cause puis réessayez.", "3회 연속 실패로 자동 저장을 중지했습니다. 변경 사항은 저장되지 않았습니다. 원인을 해결한 뒤 다시 시도하세요.", "连续三次失败，已停止自动保存。更改尚未保存。解决原因后重试。", "連續三次失敗，已停止自動儲存。變更尚未儲存。解決原因後重試。"],
        ["SavePending"] = ["設定を保存できませんでした。変更を保持して60秒後に再試行します。", "Settings could not be saved. Changes are retained for another attempt in 60 seconds.", "Einstellungen nicht gespeichert. Änderungen bleiben für einen Versuch in 60 Sekunden erhalten.", "Échec de sauvegarde. Modifications conservées pour un nouvel essai dans 60 secondes.", "설정을 저장하지 못했습니다. 변경 사항을 유지하고 60초 후 다시 시도합니다.", "无法保存设置。将保留更改并在60秒后重试。", "無法儲存設定。將保留變更並於60秒後重試。"],
        ["ConfigRecovered"] = ["設定を正常なバックアップから復元しました。", "Settings recovered from a valid backup.", "Einstellungen aus gültiger Sicherung wiederhergestellt.", "Paramètres récupérés depuis une sauvegarde valide.", "정상 백업에서 설정을 복원했습니다.", "已从有效备份恢复设置。", "已從有效備份復原設定。"],
        ["ConfigLoadWarning"] = ["設定の読込または復元保存に失敗しました。元ファイルを保持しています。設定を確認してください。", "Settings could not be loaded or repaired on disk. Original files are preserved. Please review settings.", "Einstellungen konnten nicht geladen oder repariert werden. Originaldateien bleiben erhalten. Bitte Einstellungen prüfen.", "Échec de lecture ou de réparation des paramètres. Fichiers originaux conservés. Vérifiez les paramètres.", "설정 읽기 또는 복원 저장에 실패했습니다. 원본 파일은 유지됩니다. 설정을 확인하세요.", "无法读取设置或将恢复结果写入磁盘。原始文件已保留，请检查设置。", "無法讀取設定或將復原結果寫入磁碟。原始檔案已保留，請檢查設定。"],
        ["NoLists"] = ["保存済みリストが見つかりません。", "No saved lists found.", "Keine gespeicherten Listen gefunden.", "Aucune liste enregistrée trouvée.", "저장된 목록이 없습니다.", "找不到已保存的列表。", "找不到已儲存的清單。"],
        ["NoSelection"] = ["リストを選択してください。", "Select a list.", "Wähle eine Liste.", "Sélectionnez une liste.", "목록을 선택하세요.", "请选择列表。", "請選擇清單。"],
        ["Bait"] = ["必要な餌", "Required bait", "Benötigter Köder", "Appâts requis", "필요한 미끼", "所需鱼饵", "所需魚餌"],
        ["Unknown"] = ["餌データ未登録の魚", "Fish without bait data", "Fische ohne Köderdaten", "Poissons sans données d’appât", "미끼 정보가 없는 물고기", "缺少鱼饵数据的鱼", "缺少魚餌資料的魚"],
        ["HighlightEnabled"] = ["ハイライトを有効にする", "Enable highlighting", "Hervorhebung aktivieren", "Activer le surlignage", "강조 표시 사용", "启用高亮", "啟用醒目提示"],
        ["Inventory"] = ["所持品の強調表示", "Highlight inventory", "Inventar hervorheben", "Surligner l’inventaire", "소지품 강조", "高亮背包", "醒目提示背包"],
        ["Saddlebag"] = ["チョコボかばんの強調表示", "Highlight saddlebags", "Chocobo-Taschen hervorheben", "Surligner les sacoches", "초코보 가방 강조", "高亮陆行鸟鞍囊", "醒目提示陸行鳥鞍囊"],
        ["Retainer"] = ["リテイナーの強調表示", "Highlight retainers", "Gehilfen-Inventar hervorheben", "Surligner les servants", "집사 소지품 강조", "高亮雇员背包", "醒目提示雇員背包"],
        ["ListError"] = ["リストの読み込みに失敗しました。最後に読み込めた内容を表示します。", "Could not read the list file. Showing the last valid data.", "Listendatei konnte nicht gelesen werden. Letzte gültige Daten werden angezeigt.", "Lecture du fichier impossible. Dernières données valides affichées.", "목록 파일을 읽지 못했습니다. 마지막으로 읽은 정보를 표시합니다.", "无法读取列表文件。正在显示上次有效的数据。", "無法讀取清單檔案。正在顯示上次有效資料。"],
        ["SupportText"] = ["Roxyz0501 を任意で支援できます。", "You can optionally support Roxyz0501.", "Du kannst Roxyz0501 freiwillig unterstützen.", "Vous pouvez soutenir Roxyz0501 si vous le souhaitez.", "원한다면 Roxyz0501을 후원할 수 있습니다.", "你可以自愿支持 Roxyz0501。", "你可以自願支持 Roxyz0501。"],
        ["SupportHeading"] = ["任意の支援", "Optional support", "Freiwillige Unterstützung", "Soutien facultatif", "선택적 후원", "自愿支持", "自願支持"],
        ["SupportRecipient"] = ["支援先: Roxyz0501", "Recipient: Roxyz0501", "Empfänger: Roxyz0501", "Bénéficiaire : Roxyz0501", "후원 받는 사람: Roxyz0501", "收款人：Roxyz0501", "受款人：Roxyz0501"],
        ["OpenKoFi"] = ["Ko-fi を開く", "Open Ko-fi", "Ko-fi öffnen", "Ouvrir Ko-fi", "Ko-fi 열기", "打开 Ko-fi", "開啟 Ko-fi"],
        ["None"] = ["なし", "None", "Keine", "Aucun", "없음", "无", "無"],
        ["Count"] = ["所持品内: {0}", "In inventory: {0}", "Im Inventar: {0}", "Dans l’inventaire : {0}", "소지품: {0}", "背包中：{0}", "背包中：{0}"],
    };

    public static IEnumerable<string> AllStrings => Text.Values.SelectMany(x => x);
    public static string FontText => string.Concat(AllStrings) + string.Concat(Languages.Select(x => x.Native))
        + "—…\u202F\u00A0";
    public static ClientLanguage GameDataLanguage(string? code) => code switch
    {
        "ja" => ClientLanguage.Japanese,
        "de" => ClientLanguage.German,
        "fr" => ClientLanguage.French,
        _ => ClientLanguage.English,
    };

    public static bool IsSupported(string? code) => Languages.Any(x => x.Code == code);
    public static string? FromGame(ClientLanguage? value) => value switch
    {
        ClientLanguage.Japanese => "ja",
        ClientLanguage.English => "en",
        ClientLanguage.German => "de",
        ClientLanguage.French => "fr",
        _ => null,
    };

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var value = raw.Trim().Replace('_', '-').ToLowerInvariant();
        if (value is "ja" or "ja-jp") return "ja";
        if (value is "en" or "en-us" or "en-gb") return "en";
        if (value is "de" or "de-de") return "de";
        if (value is "fr" or "fr-fr") return "fr";
        if (value is "ko" or "ko-kr") return "ko";
        if (value is "zh-hans" or "zh-cn" or "zh-sg") return "zh-Hans";
        if (value is "zh-hant" or "zh-tw" or "zh-hk" or "zh-mo") return "zh-Hant";
        return null;
    }

    public static string Get(string? code, string key)
    {
        if (!Text.TryGetValue(key, out var values)) return key;
        var index = Array.FindIndex(Languages, x => x.Code == code);
        return values[index < 0 ? 1 : index];
    }

    public static void CheckCompleteness()
    {
        foreach (var (key, values) in Text)
        {
            if (values.Length != Languages.Length || values.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException($"Incomplete localization: {key}");
            var expected = CountParameters(values[1]);
            if (values.Any(x => CountParameters(x) != expected))
                throw new InvalidOperationException($"Localization format mismatch: {key}");
        }
    }

    private static int CountParameters(string value)
        => System.Text.RegularExpressions.Regex.Matches(value, @"\{\d+\}").Count;
}
