namespace BlogArray.SaaS.OpenId.Helpers;

/// <summary>
/// Best-effort User-Agent parser based on substring/token matching.
///
/// NOT authoritative. UA strings are client-controlled, adversarial, and
/// unbounded in variety — this covers common real-world cases, not all of them.
/// If you need this for security/compliance-grade decisions rather than
/// analytics/logging, use a maintained library (e.g. UAParser.Core) instead.
///
/// KNOWN LIMITATIONS (do not "fix" these by guessing — they are unresolvable
/// from the UA string alone):
///  - Brave reports itself as Chrome. No distinguishing token exists.
///  - iPadOS 13+ defaults to a desktop Mac UA (no "iPad" token). See
///    UserAgentInfo.IsOsAmbiguous.
///  - Windows 10 and Windows 11 share NT version 10.0 and cannot be
///    distinguished via UA.
///  - Since 2025, Apple aligned Safari's version number with the OS "year"
///    branding (macOS 26 "Tahoe" ships with Safari 26). Safari jumped from
///    18.x straight to 26.x — any version-comparison logic written before
///    this change (e.g. "flag as outdated if BrowserVersion &lt; 18") is now
///    wrong. Also note BrowserVersion and OSVersion major numbers are
///    independent and should never be assumed to match just because the
///    branding looks aligned (e.g. Safari 26.0 on macOS 15.7.9 is valid).
/// </summary>
public static class UserAgentParser
{
    private const int MaxUserAgentLength = 2048;

    private static readonly TokenRule[] BrowserTokens =
    {
        // In-app / embedded browsers — check first because they commonly
        // contain Chrome, Safari, Mozilla, etc.
        new("Facebook In-App",     "FBAN",                  false),
        new("Facebook In-App",     "FB_IAB",                false),
        new("Instagram In-App",    "Instagram",             false),
        new("WeChat",              "MicroMessenger",         false),
        new("Line In-App",         "Line/",                 true),
        new("TikTok In-App",       "musical_ly",             false),
        new("Snapchat",            "Snapchat",              false),

        // Desktop / niche browsers
        new("SeaMonkey",           "SeaMonkey/"),
        new("Konqueror",           "Konqueror/"),
        new("Pale Moon",           "PaleMoon/"),
        new("Maxthon",             "Maxthon"),
        new("Vivaldi",             "Vivaldi/"),
        new("Yandex",              "YaBrowser/"),

        // Other Chromium/browser variants
        new("Huawei Browser",      "HuaweiBrowser/"),
        new("DuckDuckGo",          "DuckDuckGo/"),
        new("Chromium",            "Chromium/"),

        // Mobile browsers
        new("Samsung Internet",    "SamsungBrowser/"),
        new("UC Browser",          "UCBrowser/"),
        new("MIUI Browser",        "MiuiBrowser/"),
        new("QQ Browser",          "QQBrowser/"),
        new("Amazon Silk",         "Silk/"),

        // Opera
        new("Opera Mini",          "Opera Mini"),
        new("Opera Mobile",        "OPiOS/"),
        new("Opera",               "OPR/"),
        new("Opera",               "Opera/"),

        // Microsoft Edge — before Chrome because Edge contains Chrome
        new("Edge",                "EdgiOS/"),
        new("Edge",                "EdgA/"),
        new("Edge",                "Edg/"),

        // Chrome variants
        new("Chrome",              "CriOS/"),
        new("Chrome",              "HeadlessChrome/"),
        new("Chrome",              "Chrome/"),

        // Firefox
        new("Firefox",             "FxiOS/"),
        new("Firefox",             "Firefox/"),

        // Internet Explorer
        new("Internet Explorer",   "MSIE "),
        new("Internet Explorer",   "Trident/"),

        // Safari must be last because Chrome/Edge/etc. commonly contain Safari.
        // NOTE: version is NOT read from this token — see ExtractBrowserVersion,
        // which pulls it from "Version/" instead of the WebKit build number
        // that trails "Safari/".
        new("Safari",              "Safari/",               false)
    };

    private static readonly TokenRule[] BotTokens =
    {
        // Known bots. Tokens include the trailing "/" where the real UA has
        // one — omitting it silently breaks version extraction (ExtractVersion
        // requires digits/dots immediately after the token).
        new("Googlebot",             "Googlebot/",            true),
        new("Bingbot",               "bingbot/",              true),
        new("YandexBot",             "YandexBot/",            true),
        new("Baiduspider",           "Baiduspider/",          true),
        new("DuckDuckBot",           "DuckDuckBot/",          true),
        new("Applebot",              "Applebot/",             true),
        new("Slackbot",              "Slackbot/",              true),
        new("Discordbot",            "Discordbot/",            true),
        new("Twitterbot",            "Twitterbot/",            true),
        new("FacebookExternalHit",   "facebookexternalhit/",  true),
        new("LinkedInBot",           "LinkedInBot/",           true),
        new("WhatsApp",              "WhatsApp/",             true),
        new("AhrefsBot",             "AhrefsBot/",             true),
        new("SemrushBot",            "SemrushBot/",            true),
        new("GPTBot",                "GPTBot/",               true),
        new("ClaudeBot",             "ClaudeBot/",             true),
        new("PetalBot",              "PetalBot/",             true),

        // Generic indicators — deliberately last because they are less reliable,
        // and cover a tiny fraction of real-world crawler traffic. No version.
        new("Generic Bot/Crawler",   "bot",                   false),
        new("Generic Bot/Crawler",   "crawler",               false),
        new("Generic Bot/Crawler",   "spider",                false)
    };

    private static readonly OsRule[] OsTokens =
    {
        // More specific platforms first.
        new("Windows Phone", "Windows Phone"),
        new("Xbox",          "Xbox"),
        new("PlayStation",   "PlayStation"),
        new("Roku",          "Roku/"),
        new("Tizen",         "Tizen"),
        new("webOS",         "webOS"),
        new("Kindle",        "Kindle"),
        new("BlackBerry",    "BlackBerry"),
        new("BlackBerry",    "BB10"),

        // Apple mobile devices
        new("iOS",           "iPhone"),
        new("iOS",           "iPad"),
        new("watchOS",       "Watch OS"),

        // Android must be before Linux.
        new("Android",       "Android"),

        // Desktop platforms
        new("Windows",       "Windows NT"),
        new("ChromeOS",      "CrOS"),
        new("macOS",         "Mac OS X"),
        new("Ubuntu",        "Ubuntu"),
        new("Fedora",        "Fedora"),
        new("Linux",         "Linux"),
        new("FreeBSD",       "FreeBSD")
    };

    public enum DeviceType
    {
        Unknown,
        Desktop,
        Mobile,
        Tablet,
        TV,
        Console
    }

    public readonly struct UserAgentInfo
    {
        public string Browser { get; }
        public string BrowserVersion { get; }
        public string OS { get; }
        public string OSVersion { get; }
        public DeviceType Device { get; }
        public bool IsBot { get; }
        public string BotName { get; }

        /// <summary>
        /// True when OS was inferred as macOS from a UA that could equally be
        /// an iPad running iPadOS 13+ (which drops the "iPad" token and reports
        /// a desktop Mac UA). Callers doing device-based UX or analytics should
        /// check this rather than trusting OS == "macOS" at face value.
        /// </summary>
        public bool IsOsAmbiguous { get; }

        public UserAgentInfo(
            string browser,
            string browserVersion,
            string os,
            string osVersion,
            DeviceType device,
            bool isBot,
            string botName,
            bool isOsAmbiguous = false)
        {
            Browser = browser;
            BrowserVersion = browserVersion;
            OS = os;
            OSVersion = osVersion;
            Device = device;
            IsBot = isBot;
            BotName = botName;
            IsOsAmbiguous = isOsAmbiguous;
        }

        public override string ToString()
        {
            string browser =
                string.IsNullOrEmpty(BrowserVersion)
                    ? Browser
                    : $"{Browser} {BrowserVersion}";

            string os =
                string.IsNullOrEmpty(OSVersion)
                    ? OS
                    : $"{OS} {OSVersion}";

            if (IsOsAmbiguous)
                os += " (or iPadOS)";

            return $"{browser} on {os}";
        }
    }

    private sealed record TokenRule(
        string Name,
        string Token,
        bool ExtractVersion = true);

    private sealed record OsRule(
        string Name,
        string Token);

    public static UserAgentInfo Parse(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return new UserAgentInfo(
                "Unknown browser",
                "",
                "Unknown OS",
                "",
                DeviceType.Unknown,
                false,
                "");
        }

        // User-Agent is untrusted input. Avoid unnecessarily processing huge values.
        if (userAgent.Length > MaxUserAgentLength)
            userAgent = userAgent[..MaxUserAgentLength];

        // Bot detection has priority.
        var bot = MatchFirst(userAgent, BotTokens);

        if (bot.Name != null)
        {
            ParseOS(
                userAgent,
                out string botOs,
                out string botOsVersion,
                out bool botOsAmbiguous);

            return new UserAgentInfo(
                bot.Name,
                bot.ExtractVersion ? ExtractVersion(userAgent, bot.TokenEndIndex) : "",
                botOs,
                botOsVersion,
                DeviceType.Unknown,
                true,
                bot.Name,
                botOsAmbiguous);
        }

        var browser = MatchFirst(userAgent, BrowserTokens);

        string browserName = browser.Name ?? "Unknown browser";
        string browserVersion = "";

        if (browser.Name != null)
        {
            browserVersion = ExtractBrowserVersion(
                userAgent,
                browser.Name,
                browser.Token,
                browser.TokenEndIndex,
                browser.ExtractVersion);
        }

        ParseOS(
            userAgent,
            out string os,
            out string osVersion,
            out bool osAmbiguous);

        DeviceType device = ParseDeviceType(userAgent);

        return new UserAgentInfo(
            browserName,
            browserVersion,
            os,
            osVersion,
            device,
            false,
            "",
            osAmbiguous);
    }

    public static string DescribeUserAgent(string userAgent)
        => Parse(userAgent).ToString();

    private static BrowserMatch MatchFirst(
        string userAgent,
        TokenRule[] tokens)
    {
        foreach (TokenRule rule in tokens)
        {
            int index = userAgent.IndexOf(
                rule.Token,
                StringComparison.OrdinalIgnoreCase);

            if (index >= 0)
            {
                return new BrowserMatch(
                    rule.Name,
                    rule.Token,
                    rule.ExtractVersion,
                    index + rule.Token.Length);
            }
        }

        return default;
    }

    private static string ExtractBrowserVersion(
        string userAgent,
        string browserName,
        string token,
        int tokenEndIndex,
        bool extractVersion)
    {
        if (!extractVersion)
            return "";

        // Safari is special:
        //
        // Version/26.0 Safari/605.1.15
        //
        // "Version/26.0" = user-facing Safari browser version
        // "Safari/605.1.15" = WebKit/Safari technical build
        //
        // Therefore Safari version must come from Version/.
        if (browserName.Equals(
                "Safari",
                StringComparison.OrdinalIgnoreCase))
        {
            return ExtractVersionFromToken(userAgent, "Version/");
        }

        return ExtractVersion(userAgent, tokenEndIndex);
    }

    private static string ExtractVersionFromToken(
        string userAgent,
        string token)
    {
        int index = userAgent.IndexOf(
            token,
            StringComparison.OrdinalIgnoreCase);

        if (index < 0)
            return "";

        int start = index + token.Length;

        return ExtractNumericVersion(
            userAgent,
            start);
    }

    private static string ExtractVersion(
        string userAgent,
        int start)
    {
        if (start < 0 || start >= userAgent.Length)
            return "";

        // Tolerate a stray leading '/' in case a token was matched without
        // its trailing slash (defensive; shouldn't happen with current tables).
        if (userAgent[start] == '/')
            start++;

        return ExtractNumericVersion(userAgent, start);
    }

    private static string ExtractNumericVersion(
        string userAgent,
        int start)
    {
        int end = start;

        while (end < userAgent.Length)
        {
            char c = userAgent[end];

            if (!(char.IsDigit(c) || c == '.'))
                break;

            end++;
        }

        if (end <= start)
            return "";

        return userAgent[start..end].TrimEnd('.');
    }

    private static void ParseOS(
        string userAgent,
        out string os,
        out string osVersion,
        out bool isAmbiguous)
    {
        os = "Unknown OS";
        osVersion = "";
        isAmbiguous = false;

        foreach (OsRule rule in OsTokens)
        {
            int index = userAgent.IndexOf(
                rule.Token,
                StringComparison.OrdinalIgnoreCase);

            if (index < 0)
                continue;

            os = rule.Name;
            osVersion = ExtractOsVersion(
                userAgent,
                rule.Name,
                index);

            // iPadOS 13+ defaults to a desktop Mac UA with no "iPad" token,
            // so a "Mac OS X" match cannot be trusted as definitely macOS.
            if (rule.Name == "macOS")
                isAmbiguous = true;

            return;
        }
    }

    private static string ExtractOsVersion(
        string userAgent,
        string os,
        int tokenIndex)
    {
        switch (os)
        {
            case "Windows Phone":
                return ExtractAfterToken(
                    userAgent,
                    tokenIndex,
                    "Windows Phone");

            case "Windows":
                return ExtractAfterToken(
                    userAgent,
                    tokenIndex,
                    "Windows NT");

            case "Android":
                return ExtractAfterToken(
                    userAgent,
                    tokenIndex,
                    "Android");

            case "ChromeOS":
                return ExtractAfterToken(
                    userAgent,
                    tokenIndex,
                    "CrOS");

            case "macOS":
                return ExtractAfterToken(
                    userAgent,
                    tokenIndex,
                    "Mac OS X");

            case "iOS":
                return ExtractIOSVersion(userAgent);

            default:
                return "";
        }
    }

    private static string ExtractIOSVersion(string userAgent)
    {
        const string iPhoneToken = "CPU iPhone OS ";
        const string iPadToken = "CPU OS ";

        int index = userAgent.IndexOf(
            iPhoneToken,
            StringComparison.OrdinalIgnoreCase);

        int start = index >= 0 ? index + iPhoneToken.Length : -1;

        if (start < 0)
        {
            index = userAgent.IndexOf(
                iPadToken,
                StringComparison.OrdinalIgnoreCase);

            start = index >= 0 ? index + iPadToken.Length : -1;
        }

        if (start < 0 || start >= userAgent.Length)
            return "";

        int end = start;

        while (end < userAgent.Length)
        {
            char c = userAgent[end];

            if (!(char.IsDigit(c) || c == '.' || c == '_'))
                break;

            end++;
        }

        if (end <= start)
            return "";

        return userAgent[start..end].Replace('_', '.');
    }

    private static string ExtractAfterToken(
        string userAgent,
        int tokenIndex,
        string token)
    {
        int start = tokenIndex + token.Length;

        while (start < userAgent.Length &&
               userAgent[start] == ' ')
        {
            start++;
        }

        if (start >= userAgent.Length)
            return "";

        int end = start;

        while (end < userAgent.Length)
        {
            char c = userAgent[end];

            if (!(char.IsDigit(c) ||
                  char.IsLetter(c) ||
                  c == '.' ||
                  c == '_' ||
                  c == '-'))
            {
                break;
            }

            end++;
        }

        if (end <= start)
            return "";

        return userAgent[start..end].Replace('_', '.');
    }

    private static DeviceType ParseDeviceType(string userAgent)
    {
        // Explicit consoles
        if (Contains(userAgent, "Xbox") ||
            Contains(userAgent, "PlayStation"))
        {
            return DeviceType.Console;
        }

        // TVs
        if (Contains(userAgent, "SmartTV") ||
            Contains(userAgent, "HbbTV") ||
            Contains(userAgent, "Tizen") ||
            Contains(userAgent, "webOS") ||
            Contains(userAgent, "Roku"))
        {
            return DeviceType.TV;
        }

        // Firefox/Android and many mobile browsers identify themselves
        // with "Mobile" or "Tablet".
        if (Contains(userAgent, "Tablet"))
            return DeviceType.Tablet;

        if (Contains(userAgent, "Mobi"))
            return DeviceType.Mobile;

        // iPadOS desktop-style UA may not contain "Mobi".
        // Explicit iPad remains a strong tablet signal.
        if (Contains(userAgent, "iPad"))
            return DeviceType.Tablet;

        // Android UAs conventionally omit the "Mobile" token specifically to
        // signal a tablet form factor (this is Android's own UA convention,
        // not a guess) — since "Mobi" was already ruled out above, an
        // Android UA reaching this point is a tablet, not a phone.
        if (Contains(userAgent, "Android"))
            return DeviceType.Tablet;

        if (Contains(userAgent, "Windows NT") ||
            Contains(userAgent, "Macintosh") ||
            Contains(userAgent, "X11") ||
            Contains(userAgent, "CrOS"))
        {
            return DeviceType.Desktop;
        }

        return DeviceType.Unknown;
    }

    private static bool Contains(
        string value,
        string token)
    {
        return value.IndexOf(
                   token,
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private readonly struct BrowserMatch
    {
        public string Name { get; }
        public string Token { get; }
        public bool ExtractVersion { get; }
        public int TokenEndIndex { get; }

        public BrowserMatch(
            string name,
            string token,
            bool extractVersion,
            int tokenEndIndex)
        {
            Name = name;
            Token = token;
            ExtractVersion = extractVersion;
            TokenEndIndex = tokenEndIndex;
        }
    }
}

