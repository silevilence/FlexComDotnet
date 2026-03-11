using FlexComDotnet.Core.Features.EmojiSupport.Models;

namespace FlexComDotnet.Core.Features.EmojiSupport.Services;

/// <summary>
/// Emoji 服务 - 内置常用 Emoji 短码映射
/// </summary>
public class EmojiService : IEmojiService
{
    private readonly List<EmojiEntry> _entries;
    private readonly Dictionary<string, EmojiEntry> _shortcodeMap;

    public EmojiService()
    {
        _entries = BuildEmojiDatabase();
        _shortcodeMap = _entries.ToDictionary(e => e.Shortcode, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<EmojiEntry> Search(string prefix, int maxResults = 10)
    {
        if (string.IsNullOrEmpty(prefix))
            return [];

        var normalizedPrefix = prefix.ToLowerInvariant();

        // 优先完全前缀匹配，再包含匹配
        var prefixMatches = _entries
            .Where(e => e.Shortcode.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
            .Take(maxResults)
            .ToList();

        if (prefixMatches.Count >= maxResults)
            return prefixMatches;

        var containsMatches = _entries
            .Where(e => !e.Shortcode.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase)
                        && e.Shortcode.Contains(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
            .Take(maxResults - prefixMatches.Count);

        return [.. prefixMatches, .. containsMatches];
    }

    public EmojiEntry? GetByShortcode(string shortcode)
    {
        _shortcodeMap.TryGetValue(shortcode, out var entry);
        return entry;
    }

    public IReadOnlyList<EmojiEntry> GetAll() => _entries;

    private static List<EmojiEntry> BuildEmojiDatabase()
    {
        return
        [
            // 😀 表情
            E("😀", "grinning", "表情"),
            E("😃", "smiley", "表情"),
            E("😄", "smile", "表情"),
            E("😁", "grin", "表情"),
            E("😆", "laughing", "表情"),
            E("😅", "sweat_smile", "表情"),
            E("🤣", "rofl", "表情"),
            E("😂", "joy", "表情"),
            E("🙂", "slightly_smiling_face", "表情"),
            E("😉", "wink", "表情"),
            E("😊", "blush", "表情"),
            E("😇", "innocent", "表情"),
            E("🥰", "smiling_face_with_hearts", "表情"),
            E("😍", "heart_eyes", "表情"),
            E("🤩", "star_struck", "表情"),
            E("😘", "kissing_heart", "表情"),
            E("😋", "yum", "表情"),
            E("😜", "stuck_out_tongue_winking_eye", "表情"),
            E("🤓", "nerd_face", "表情"),
            E("😎", "sunglasses", "表情"),
            E("🤗", "hugs", "表情"),
            E("🤔", "thinking", "表情"),
            E("🤫", "shushing_face", "表情"),
            E("🤭", "hand_over_mouth", "表情"),
            E("😐", "neutral_face", "表情"),
            E("😑", "expressionless", "表情"),
            E("😶", "no_mouth", "表情"),
            E("😏", "smirk", "表情"),
            E("😒", "unamused", "表情"),
            E("🙄", "roll_eyes", "表情"),
            E("😬", "grimacing", "表情"),
            E("😮‍💨", "exhaling", "表情"),
            E("🤥", "lying_face", "表情"),
            E("😌", "relieved", "表情"),
            E("😔", "pensive", "表情"),
            E("😪", "sleepy", "表情"),
            E("🤤", "drooling_face", "表情"),
            E("😴", "sleeping", "表情"),
            E("😷", "mask", "表情"),
            E("🤒", "face_with_thermometer", "表情"),
            E("🤕", "head_bandage", "表情"),
            E("🤢", "nauseated_face", "表情"),
            E("🤮", "vomiting", "表情"),
            E("😵", "dizzy_face", "表情"),
            E("🥶", "cold_face", "表情"),
            E("🥵", "hot_face", "表情"),
            E("😱", "scream", "表情"),
            E("😨", "fearful", "表情"),
            E("😰", "cold_sweat", "表情"),
            E("😥", "disappointed_relieved", "表情"),
            E("😢", "cry", "表情"),
            E("😭", "sob", "表情"),
            E("😤", "triumph", "表情"),
            E("😠", "angry", "表情"),
            E("😡", "rage", "表情"),
            E("🤬", "cursing_face", "表情"),
            E("👿", "imp", "表情"),
            E("💀", "skull", "表情"),
            E("💩", "poop", "表情"),
            E("🤡", "clown_face", "表情"),
            E("👹", "ogre", "表情"),
            E("👻", "ghost", "表情"),
            E("👽", "alien", "表情"),
            E("🤖", "robot", "表情"),

            // 👋 手势
            E("👋", "wave", "手势"),
            E("🤚", "raised_back_of_hand", "手势"),
            E("✋", "hand", "手势"),
            E("🖖", "vulcan_salute", "手势"),
            E("👌", "ok_hand", "手势"),
            E("🤌", "pinched_fingers", "手势"),
            E("✌️", "v", "手势"),
            E("🤞", "crossed_fingers", "手势"),
            E("🤟", "love_you_gesture", "手势"),
            E("🤘", "metal", "手势"),
            E("🤙", "call_me_hand", "手势"),
            E("👈", "point_left", "手势"),
            E("👉", "point_right", "手势"),
            E("👆", "point_up_2", "手势"),
            E("👇", "point_down", "手势"),
            E("☝️", "point_up", "手势"),
            E("👍", "thumbsup", "手势"),
            E("👎", "thumbsdown", "手势"),
            E("✊", "fist", "手势"),
            E("👊", "facepunch", "手势"),
            E("🤝", "handshake", "手势"),
            E("👏", "clap", "手势"),
            E("🙌", "raised_hands", "手势"),
            E("💪", "muscle", "手势"),
            E("🙏", "pray", "手势"),

            // ❤️ 心形与爱
            E("❤️", "heart", "心形"),
            E("🧡", "orange_heart", "心形"),
            E("💛", "yellow_heart", "心形"),
            E("💚", "green_heart", "心形"),
            E("💙", "blue_heart", "心形"),
            E("💜", "purple_heart", "心形"),
            E("🖤", "black_heart", "心形"),
            E("🤍", "white_heart", "心形"),
            E("💔", "broken_heart", "心形"),
            E("💕", "two_hearts", "心形"),
            E("💖", "sparkling_heart", "心形"),
            E("💗", "heartpulse", "心形"),
            E("💘", "cupid", "心形"),
            E("💝", "gift_heart", "心形"),

            // ⭐ 符号
            E("⭐", "star", "符号"),
            E("🌟", "star2", "符号"),
            E("✨", "sparkles", "符号"),
            E("💫", "dizzy", "符号"),
            E("🔥", "fire", "符号"),
            E("💥", "boom", "符号"),
            E("💯", "100", "符号"),
            E("🎉", "tada", "符号"),
            E("🎊", "confetti_ball", "符号"),
            E("🎈", "balloon", "符号"),
            E("🏆", "trophy", "符号"),
            E("🥇", "1st_place_medal", "符号"),
            E("🥈", "2nd_place_medal", "符号"),
            E("🥉", "3rd_place_medal", "符号"),
            E("🎯", "dart", "符号"),
            E("🔔", "bell", "符号"),
            E("🔕", "no_bell", "符号"),
            E("🎵", "musical_note", "符号"),
            E("🎶", "notes", "符号"),

            // ✅ 状态标记
            E("✅", "white_check_mark", "状态"),
            E("☑️", "ballot_box_with_check", "状态"),
            E("✔️", "heavy_check_mark", "状态"),
            E("❌", "x", "状态"),
            E("❎", "negative_squared_cross_mark", "状态"),
            E("⚠️", "warning", "状态"),
            E("⛔", "no_entry", "状态"),
            E("🚫", "no_entry_sign", "状态"),
            E("💡", "bulb", "状态"),
            E("🔍", "mag", "状态"),
            E("🔎", "mag_right", "状态"),
            E("📝", "memo", "状态"),
            E("📌", "pushpin", "状态"),
            E("📎", "paperclip", "状态"),
            E("🔗", "link", "状态"),
            E("🔒", "lock", "状态"),
            E("🔓", "unlock", "状态"),
            E("🔑", "key", "状态"),
            E("🛠️", "hammer_and_wrench", "状态"),
            E("⚙️", "gear", "状态"),
            E("🧪", "test_tube", "状态"),
            E("🧬", "dna", "状态"),
            E("🏷️", "label", "状态"),
            E("📋", "clipboard", "状态"),

            // 📦 物品
            E("📦", "package", "物品"),
            E("📁", "file_folder", "物品"),
            E("📂", "open_file_folder", "物品"),
            E("📄", "page_facing_up", "物品"),
            E("📃", "page_with_curl", "物品"),
            E("📊", "bar_chart", "物品"),
            E("📈", "chart_with_upwards_trend", "物品"),
            E("📉", "chart_with_downwards_trend", "物品"),
            E("💻", "computer", "物品"),
            E("🖥️", "desktop_computer", "物品"),
            E("⌨️", "keyboard", "物品"),
            E("🖱️", "computer_mouse", "物品"),
            E("💾", "floppy_disk", "物品"),
            E("💿", "cd", "物品"),
            E("📡", "satellite", "物品"),
            E("🔌", "electric_plug", "物品"),
            E("🔋", "battery", "物品"),
            E("📱", "iphone", "物品"),
            E("📞", "telephone_receiver", "物品"),
            E("📧", "email", "物品"),
            E("📬", "mailbox_with_mail", "物品"),

            // 🚀 交通
            E("🚀", "rocket", "交通"),
            E("✈️", "airplane", "交通"),
            E("🚗", "car", "交通"),
            E("🚕", "taxi", "交通"),
            E("🚌", "bus", "交通"),
            E("🚲", "bike", "交通"),

            // ⏰ 时间
            E("⏰", "alarm_clock", "时间"),
            E("⏱️", "stopwatch", "时间"),
            E("⏳", "hourglass_flowing_sand", "时间"),
            E("⌛", "hourglass", "时间"),
            E("🕐", "clock1", "时间"),

            // 🌈 自然
            E("🌈", "rainbow", "自然"),
            E("☀️", "sunny", "自然"),
            E("🌙", "crescent_moon", "自然"),
            E("⛅", "partly_sunny", "自然"),
            E("🌧️", "cloud_with_rain", "自然"),
            E("❄️", "snowflake", "自然"),
            E("🌊", "ocean", "自然"),
            E("🌸", "cherry_blossom", "自然"),
            E("🌹", "rose", "自然"),
            E("🌻", "sunflower", "自然"),
            E("🌲", "evergreen_tree", "自然"),
            E("🍀", "four_leaf_clover", "自然"),

            // 🍕 食物
            E("🍕", "pizza", "食物"),
            E("🍔", "hamburger", "食物"),
            E("🍟", "fries", "食物"),
            E("🍦", "icecream", "食物"),
            E("🍩", "doughnut", "食物"),
            E("🎂", "birthday", "食物"),
            E("🍰", "cake", "食物"),
            E("☕", "coffee", "食物"),
            E("🍵", "tea", "食物"),
            E("🍺", "beer", "食物"),
            E("🍷", "wine_glass", "食物"),

            // 🐱 动物
            E("🐱", "cat", "动物"),
            E("🐶", "dog", "动物"),
            E("🐻", "bear", "动物"),
            E("🐼", "panda_face", "动物"),
            E("🐨", "koala", "动物"),
            E("🦊", "fox_face", "动物"),
            E("🦁", "lion", "动物"),
            E("🐸", "frog", "动物"),
            E("🐙", "octopus", "动物"),
            E("🦋", "butterfly", "动物"),
            E("🐝", "bee", "动物"),
            E("🐛", "bug", "动物"),
            E("🐞", "ladybug", "动物"),
            E("🦀", "crab", "动物"),
            E("🐳", "whale", "动物"),
            E("🐬", "dolphin", "动物"),

            // 🔢 数字与箭头
            E("0️⃣", "zero", "数字"),
            E("1️⃣", "one", "数字"),
            E("2️⃣", "two", "数字"),
            E("3️⃣", "three", "数字"),
            E("4️⃣", "four", "数字"),
            E("5️⃣", "five", "数字"),
            E("6️⃣", "six", "数字"),
            E("7️⃣", "seven", "数字"),
            E("8️⃣", "eight", "数字"),
            E("9️⃣", "nine", "数字"),
            E("🔟", "keycap_ten", "数字"),
            E("➡️", "arrow_right", "箭头"),
            E("⬅️", "arrow_left", "箭头"),
            E("⬆️", "arrow_up", "箭头"),
            E("⬇️", "arrow_down", "箭头"),
            E("↩️", "leftwards_arrow_with_hook", "箭头"),
            E("🔄", "arrows_counterclockwise", "箭头"),
            E("🔀", "twisted_rightwards_arrows", "箭头"),
            E("➕", "heavy_plus_sign", "符号"),
            E("➖", "heavy_minus_sign", "符号"),
            E("✖️", "heavy_multiplication_x", "符号"),
            E("➗", "heavy_division_sign", "符号"),
            E("♾️", "infinity", "符号"),
            E("💲", "heavy_dollar_sign", "符号"),

            // 🏠 地点
            E("🏠", "house", "地点"),
            E("🏢", "office", "地点"),
            E("🏫", "school", "地点"),
            E("🏥", "hospital", "地点"),
            E("🏭", "factory", "地点"),

            // 🎮 活动
            E("🎮", "video_game", "活动"),
            E("🎲", "game_die", "活动"),
            E("🎳", "bowling", "活动"),
            E("🎸", "guitar", "活动"),
            E("🎺", "trumpet", "活动"),
            E("🎨", "art", "活动"),
            E("📷", "camera", "活动"),
            E("🎬", "clapper", "活动"),
            E("📺", "tv", "活动"),
            E("📻", "radio", "活动"),

            // 🚩 旗帜
            E("🏁", "checkered_flag", "旗帜"),
            E("🚩", "triangular_flag_on_post", "旗帜"),
            E("🏴", "black_flag", "旗帜"),
            E("🏳️", "white_flag", "旗帜"),
        ];
    }

    private static EmojiEntry E(string emoji, string shortcode, string category)
        => new() { Emoji = emoji, Shortcode = shortcode, Category = category };
}
