using System;
using System.Globalization;

namespace SimpleCities.Core.V3;

/// <summary>
/// V3 严格 reader 的 JSON lexeme 基础校验。
/// 整数 token：无正号、无前导零、无小数点/指数、无 `-0`；
/// float token：必须可解析为有限 binary32，拒绝字符串化数字、NaN/Infinity。
/// </summary>
public static class V3JsonLexeme
{
    public static bool IsValidCanonicalInteger(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        if (token == "0")
            return true;
        if (token[0] == '0')
            return false;

        foreach (char c in token)
        {
            if (c < '0' || c > '9')
                return false;
        }

        return true;
    }

    public static bool IsValidFiniteFloatLexeme(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            return false;

        return float.IsFinite(value);
    }
}
