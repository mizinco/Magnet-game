using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerPrefs ベースの簡易ランキング（上位 N 件）。
/// 1 件 = 名前 + スコア + 日付（YYYY-MM-DD）。
///
/// PlayerPrefs キー：
///   MagnetGame_Rank_Count        … 現在の件数（0〜MAX）
///   MagnetGame_Rank_{i}_Name     … i 番目の名前
///   MagnetGame_Rank_{i}_Score    … i 番目のスコア
///   MagnetGame_Rank_{i}_Date     … i 番目の日付（YYYY-MM-DD）
///
/// インデックス 0 = 1位（高い順）。
/// </summary>
public static class Ranking
{
    public const int MAX = 5;

    private const string KEY_COUNT = "MagnetGame_Rank_Count";
    private const string KEY_NAME_FMT = "MagnetGame_Rank_{0}_Name";
    private const string KEY_SCORE_FMT = "MagnetGame_Rank_{0}_Score";
    private const string KEY_DATE_FMT = "MagnetGame_Rank_{0}_Date";

    // ────────────────────────────────────────────
    // 公開 API
    // ────────────────────────────────────────────
    public struct Entry
    {
        public string name;
        public int score;
        public string date;   // YYYY-MM-DD
    }

    /// <summary>現在のランキングを高い順で取得（最大 MAX 件）。</summary>
    public static List<Entry> Load()
    {
        var list = new List<Entry>(MAX);
        int count = Mathf.Clamp(PlayerPrefs.GetInt(KEY_COUNT, 0), 0, MAX);
        for (int i = 0; i < count; i++)
        {
            list.Add(new Entry
            {
                name = PlayerPrefs.GetString(string.Format(KEY_NAME_FMT, i), "---"),
                score = PlayerPrefs.GetInt(string.Format(KEY_SCORE_FMT, i), 0),
                date = PlayerPrefs.GetString(string.Format(KEY_DATE_FMT, i), "----/--/--"),
            });
        }
        return list;
    }

    /// <summary>
    /// 新しいスコアがランクインするかを判定（保存はしない）
    /// </summary>
    public static bool IsRankIn(int score)
    {
        // 件数が MAX 未満なら確実にランクイン
        var list = Load();
        if (list.Count < MAX) return true;
        return score > list[list.Count - 1].score;
    }

    /// <summary>
    /// エントリを追加して保存する。何位に入ったかを返す（1-indexed）。
    /// ランク外なら -1 を返す。
    /// </summary>
    public static int Submit(string name, int score)
    {
        var list = Load();
        var entry = new Entry
        {
            name = string.IsNullOrWhiteSpace(name) ? "AAA" : name.Trim(),
            score = score,
            date = DateTime.Now.ToString("yyyy-MM-dd"),
        };

        // 高い順で挿入位置を決める（同点なら新しい方を後ろ）
        int insertAt = list.Count;
        for (int i = 0; i < list.Count; i++)
        {
            if (entry.score > list[i].score) { insertAt = i; break; }
        }

        if (insertAt >= MAX) return -1; // ランク外

        list.Insert(insertAt, entry);
        if (list.Count > MAX) list.RemoveAt(MAX);

        Save(list);
        return insertAt + 1;
    }

    /// <summary>全件削除。</summary>
    public static void Clear()
    {
        for (int i = 0; i < MAX; i++)
        {
            PlayerPrefs.DeleteKey(string.Format(KEY_NAME_FMT, i));
            PlayerPrefs.DeleteKey(string.Format(KEY_SCORE_FMT, i));
            PlayerPrefs.DeleteKey(string.Format(KEY_DATE_FMT, i));
        }
        PlayerPrefs.SetInt(KEY_COUNT, 0);
        PlayerPrefs.Save();
        Debug.Log("[Ranking] 全件クリアしました。");
    }

    private static void Save(List<Entry> list)
    {
        int count = Mathf.Min(list.Count, MAX);
        PlayerPrefs.SetInt(KEY_COUNT, count);
        for (int i = 0; i < count; i++)
        {
            PlayerPrefs.SetString(string.Format(KEY_NAME_FMT, i), list[i].name);
            PlayerPrefs.SetInt(string.Format(KEY_SCORE_FMT, i), list[i].score);
            PlayerPrefs.SetString(string.Format(KEY_DATE_FMT, i), list[i].date);
        }
        PlayerPrefs.Save();
    }

    // ===== 列別取得（4 列 Text 分割表示用） =====
    // 各メソッドは複数行文字列を返す。同じ list を渡せば各列が同じ行数になり、
    // 上から順位 1 位、2 位、… と並ぶ。プロポーショナルフォントでも各列単独で
    // 左端（または右端）が揃うため、フォント依存がなくなる。

    /// <summary>順位列（"1", "2", ...）を改行区切りで返す</summary>
    public static string FormatRanks(List<Entry> list)  => JoinLines(list, (e, i) => (i + 1).ToString());

    /// <summary>名前列を改行区切りで返す</summary>
    public static string FormatNames(List<Entry> list)  => JoinLines(list, (e, i) => e.name ?? "");

    /// <summary>スコア列を改行区切りで返す</summary>
    public static string FormatScores(List<Entry> list) => JoinLines(list, (e, i) => e.score.ToString());

    /// <summary>日付列を改行区切りで返す</summary>
    public static string FormatDates(List<Entry> list)  => JoinLines(list, (e, i) => e.date ?? "");

    private static string JoinLines(List<Entry> list, Func<Entry, int, string> selector)
    {
        if (list == null || list.Count == 0) return "";
        var lines = new string[list.Count];
        for (int i = 0; i < list.Count; i++) lines[i] = selector(list[i], i);
        return string.Join("\n", lines);
    }
}
