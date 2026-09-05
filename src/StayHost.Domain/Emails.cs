namespace StayHost.Domain;

/// <summary>
/// docs/01 TK-09, the server half — what an email says around its content, in
/// the reader's own language.
///
/// Everything here is translated BY HAND, and the split with machine
/// translation is a hard rule, not a preference: the frame (greeting, link
/// label, sign-off) and the secret-bearing templates (a sign-in code, a reset
/// link) must be exact, because a machine that "improves" one digit of an OTP
/// locks a person out of their account with no error anywhere. Informational
/// notification bodies go through the machine at dispatch time instead
/// (EmailDispatcher), where a bad day at the translator costs nothing — the
/// Vietnamese original is the designed fallback.
///
/// The name sits inside each greeting template, not appended to it: Korean and
/// Japanese put the name before the honorific, and gluing "Chào" + name broke
/// exactly this way once before (CLAUDE.md §4, "Message Binn").
/// </summary>
public static class Emails
{
    private sealed record Frame(
        string Greeting,      // {0} = the reader's name
        string Cta,           // label in front of the link
        string SignOff,
        string MachineNote);  // appended only when the body was machine-translated

    private static readonly Dictionary<string, Frame> Frames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vi"] = new("Chào {0},", "Xem chi tiết:", "— Đội ngũ Staylio", ""),
        ["en"] = new("Hi {0},", "View details:", "— The Staylio team",
                     "Automatically translated from Vietnamese."),
        ["ja"] = new("{0} 様", "詳細はこちら：", "— Staylio チーム",
                     "このメールはベトナム語から自動翻訳されています。"),
        ["ko"] = new("{0}님, 안녕하세요.", "자세히 보기:", "— Staylio 팀",
                     "이 메일은 베트남어에서 자동 번역되었습니다."),
        ["zh"] = new("{0}，您好：", "查看详情：", "— Staylio 团队",
                     "本邮件由越南语自动翻译。"),
        ["fr"] = new("Bonjour {0},", "Voir les détails :", "— L'équipe Staylio",
                     "Traduction automatique du vietnamien."),
        ["de"] = new("Hallo {0},", "Details ansehen:", "— Das Staylio-Team",
                     "Automatisch aus dem Vietnamesischen übersetzt."),
        ["es"] = new("Hola, {0}:", "Ver detalles:", "— El equipo de Staylio",
                     "Traducción automática del vietnamita."),
    };

    /// <summary>Null, unknown or unsupported all mean Vietnamese — today's behaviour.</summary>
    private static Frame FrameFor(string? lang) =>
        Frames.TryGetValue((lang ?? "").Trim(), out var f) ? f : Frames["vi"];

    /// <summary>The languages a frame exists for. Must cover every UI language.</summary>
    public static bool Covers(string lang) => Frames.ContainsKey(lang);

    /// <summary>
    /// The whole mail around its content. For "vi" this produces byte-for-byte
    /// what BuildEmailBody always produced, so the 438 queued mails that carry
    /// that exact frame are the proof of parity.
    /// </summary>
    public static string Compose(
        string? lang, string name, string title, string body, string? url,
        bool machineTranslated = false)
    {
        var f = FrameFor(lang);
        var cta = url is null ? "" : $"\n\n{f.Cta} {url}";
        var note = machineTranslated && f.MachineNote.Length > 0 ? $"\n\n{f.MachineNote}" : "";
        return $"{string.Format(f.Greeting, name)}\n\n{title}\n\n{body}{cta}{note}\n\n{f.SignOff}";
    }

    /* --------------------------------------------- secret-bearing templates */

    /// <summary>
    /// docs/01 TK-01/TK-08 — the six-digit code. No digits in any language's
    /// subject: subjects show on lock screens and in mail logs, and
    /// EmailDelivery.SubjectLeaksCode stands guard over every one of these.
    /// </summary>
    public static string OtpSubject(string? lang) => (lang ?? "vi").Trim().ToLowerInvariant() switch
    {
        "en" => "Your Staylio verification code",
        "ja" => "Staylio 認証コード",
        "ko" => "Staylio 인증 코드",
        "zh" => "Staylio 验证码",
        "fr" => "Code de vérification Staylio",
        "de" => "Staylio-Bestätigungscode",
        "es" => "Código de verificación de Staylio",
        // Through EmailDelivery so the Vietnamese subject has exactly one home.
        _ => EmailDelivery.CodeSubject(),
    };

    public static string OtpBody(string? lang, string code, int minutes) =>
        (lang ?? "vi").Trim().ToLowerInvariant() switch
        {
            "en" => $"Your verification code is {code}. It is valid for {minutes} minutes.",
            "ja" => $"認証コードは {code} です。有効期限は {minutes} 分です。",
            "ko" => $"인증 코드는 {code} 입니다. 유효 시간은 {minutes}분입니다.",
            "zh" => $"你的验证码是 {code}，有效期 {minutes} 分钟。",
            "fr" => $"Votre code de vérification est {code}. Il est valable {minutes} minutes.",
            "de" => $"Ihr Bestätigungscode lautet {code}. Er ist {minutes} Minuten gültig.",
            "es" => $"Tu código de verificación es {code}. Es válido durante {minutes} minutos.",
            _ => $"Mã xác thực của bạn là {code}. Mã có hiệu lực trong {minutes} phút.",
        };

    /// <summary>docs/01 TK-08 — the reset link. The link goes through verbatim.</summary>
    public static string ResetSubject(string? lang) => (lang ?? "vi").Trim().ToLowerInvariant() switch
    {
        "en" => "Reset your Staylio password",
        "ja" => "Staylio パスワードの再設定",
        "ko" => "Staylio 비밀번호 재설정",
        "zh" => "重置 Staylio 密码",
        "fr" => "Réinitialisation du mot de passe Staylio",
        "de" => "Staylio-Passwort zurücksetzen",
        "es" => "Restablecer tu contraseña de Staylio",
        _ => "Đặt lại mật khẩu Staylio",
    };

    public static string ResetBody(string? lang, string link) =>
        (lang ?? "vi").Trim().ToLowerInvariant() switch
        {
            "en" => $"You asked to reset your password. Open this link within 2 hours:\n{link}\n\n"
                    + "If this wasn't you, ignore this email — your current password is unchanged.",
            "ja" => $"パスワードの再設定がリクエストされました。2時間以内に次のリンクを開いてください：\n{link}\n\n"
                    + "心当たりがない場合はこのメールを無視してください。現在のパスワードはそのままです。",
            "ko" => $"비밀번호 재설정을 요청하셨습니다. 2시간 안에 아래 링크를 열어 주세요:\n{link}\n\n"
                    + "본인이 요청하지 않았다면 이 메일을 무시하세요. 현재 비밀번호는 그대로입니다.",
            "zh" => $"你请求了重置密码。请在 2 小时内打开以下链接：\n{link}\n\n"
                    + "如果这不是你的操作，请忽略此邮件——当前密码保持不变。",
            "fr" => $"Vous avez demandé à réinitialiser votre mot de passe. Ouvrez ce lien sous 2 heures :\n{link}\n\n"
                    + "Si ce n'était pas vous, ignorez ce message — votre mot de passe actuel reste inchangé.",
            "de" => $"Sie haben das Zurücksetzen Ihres Passworts angefordert. Öffnen Sie diesen Link innerhalb von 2 Stunden:\n{link}\n\n"
                    + "Falls Sie das nicht waren, ignorieren Sie diese E-Mail — Ihr aktuelles Passwort bleibt unverändert.",
            "es" => $"Has pedido restablecer tu contraseña. Abre este enlace en un plazo de 2 horas:\n{link}\n\n"
                    + "Si no fuiste tú, ignora este correo: tu contraseña actual sigue igual.",
            _ => $"Bạn vừa yêu cầu đặt lại mật khẩu. Mở liên kết sau trong 2 giờ:\n{link}\n\n"
                 + "Nếu không phải bạn yêu cầu, hãy bỏ qua thư này — mật khẩu hiện tại vẫn nguyên.",
        };
}
