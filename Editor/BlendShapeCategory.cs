using System.Collections.Generic;
using System.Text;

namespace BlendShapeFilter
{
    /// <summary>
    /// Face part a BlendShape appears to belong to, guessed from its name.
    /// This is a display grouping only: the Mesh is never inspected or modified.
    /// </summary>
    public enum BlendShapeCategory
    {
        Other = 0,
        Eye,
        Brow,
        Mouth,
        Tongue,
        Teeth,
        Nose,
        Cheek,
        Jaw,
        Ear,
    }

    /// <summary>
    /// Finer grained part inside Eye and Mouth. Those two are the only groups that grow large
    /// enough on a typical avatar to be worth splitting; every other face part (Brow included)
    /// has too few BlendShapes for a split to be useful.
    /// Each parent ends with a catch all so no BlendShape falls out of its group.
    /// </summary>
    public enum BlendShapeSubCategory
    {
        None = 0,

        EyeLid,
        EyeIris,
        EyeLash,
        EyeGaze,
        EyeShape,

        MouthViseme,
        MouthLip,
        MouthCorner,
        MouthShape,
    }

    /// <summary>
    /// Guesses the face part of a BlendShape from its name.
    ///
    /// Names in the wild follow several conventions, so each rule carries three kinds of
    /// pattern:
    ///   ExactNames - the whole name must match, used for one character MMD morphs where a
    ///                partial match would hit unrelated names.
    ///   Keywords   - case insensitive partial match, used for distinctive words.
    ///   Tokens     - matches a whole word only, used for short words such as "ear" that
    ///                would otherwise match "tear" or "beard".
    ///
    /// Rules are evaluated in order, so the more specific part wins: "eyebrow" is a Brow,
    /// not an Eye, and "Mouth_Tongue_Out" is a Tongue.
    /// </summary>
    public static class BlendShapeCategoryClassifier
    {
        private class CategoryRule
        {
            public BlendShapeCategory Category;
            public string[] ExactNames;
            public string[] Keywords;
            public string[] Tokens;

            public CategoryRule(BlendShapeCategory category, string[] exactNames, string[] keywords, string[] tokens)
            {
                Category = category;
                ExactNames = exactNames;
                Keywords = keywords;
                Tokens = tokens;
            }
        }

        private static readonly string[] NoPatterns = new string[0];

        /// <summary>
        /// Evaluation order. Brow is tested before Eye so "eyebrow" is not read as an eye,
        /// and Mouth is tested last so tongue, teeth and jaw win over a generic mouth name.
        /// </summary>
        private static readonly CategoryRule[] Rules =
        {
            new CategoryRule(
                BlendShapeCategory.Brow,
                NoPatterns,
                // "brw" covers VRoid Fcl_BRW_*. The two MMD morphs are brow morphs whose
                // names contain other body parts, so they are matched here first.
                new[] { "eyebrow", "brw", "眉", "まゆ", "マユ", "真面目", "困る" },
                // "brow" on its own is a whole word match so "Hair_Brown" is not a brow.
                new[] { "brow", "brows", "mayu" }),

            new CategoryRule(
                BlendShapeCategory.Eye,
                NoPatterns,
                new[]
                {
                    "blink", "wink", "iris", "pupil", "eyelash", "eyelid", "gaze",
                    "目", "眼", "瞳", "瞼", "まぶた", "まばたき", "瞬き", "睫",
                    "まつ毛", "まつげ", "視線", "目線",
                    "ウィンク", "ウインク", "ｳｨﾝｸ",
                },
                // "eye" and "lash" are whole word matches so "Eyewear_Hide" and "Flash"
                // are not read as eye shapes.
                new[] { "eye", "hitomi", "lash", "lashes" }),

            new CategoryRule(
                BlendShapeCategory.Tongue,
                NoPatterns,
                new[] { "tongue", "舌", "べろ", "ベロ" },
                new[] { "bero" }),

            new CategoryRule(
                BlendShapeCategory.Teeth,
                NoPatterns,
                // "fcl_ha" is the VRoid teeth prefix; a bare "ha" would match far too much.
                new[] { "teeth", "tooth", "歯", "fcl_ha" },
                NoPatterns),

            new CategoryRule(
                BlendShapeCategory.Nose,
                NoPatterns,
                new[] { "nose", "sneer", "鼻" },
                NoPatterns),

            new CategoryRule(
                BlendShapeCategory.Cheek,
                NoPatterns,
                new[] { "cheek", "頬", "ほほ", "ほお", "チーク" },
                NoPatterns),

            new CategoryRule(
                BlendShapeCategory.Jaw,
                NoPatterns,
                new[] { "jaw", "chin", "顎", "あご", "アゴ" },
                new[] { "ago" }),

            new CategoryRule(
                BlendShapeCategory.Ear,
                NoPatterns,
                new[] { "耳", "みみ" },
                new[] { "ear", "ears", "mimi" }),

            new CategoryRule(
                BlendShapeCategory.Mouth,
                // Standard MMD vowel and mouth shape morphs. Their names are a single
                // character, so only a whole name match is safe.
                new[] { "あ", "い", "う", "え", "お", "ワ", "ω", "▲", "∧", "にやり" },
                new[]
                {
                    "mouth", "mth", "viseme", "vrc.v_",
                    "口", "唇", "くち", "クチ", "リップ",
                },
                // "lip" is a whole word match so "Near_Clip" is not a mouth.
                new[] { "lip", "lips", "kuchi" }),
        };

        /// <summary>Order the face part buttons are shown in.</summary>
        public static readonly BlendShapeCategory[] DisplayOrder =
        {
            BlendShapeCategory.Eye,
            BlendShapeCategory.Brow,
            BlendShapeCategory.Mouth,
            BlendShapeCategory.Tongue,
            BlendShapeCategory.Teeth,
            BlendShapeCategory.Nose,
            BlendShapeCategory.Cheek,
            BlendShapeCategory.Jaw,
            BlendShapeCategory.Ear,
            BlendShapeCategory.Other,
        };

        public static int CategoryCount
        {
            get { return DisplayOrder.Length; }
        }

        private class SubCategoryRule
        {
            public BlendShapeSubCategory SubCategory;
            public string[] ExactNames;
            public string[] Keywords;
            public string[] Tokens;

            public SubCategoryRule(
                BlendShapeSubCategory subCategory, string[] exactNames, string[] keywords, string[] tokens)
            {
                SubCategory = subCategory;
                ExactNames = exactNames;
                Keywords = keywords;
                Tokens = tokens;
            }
        }

        private static readonly SubCategoryRule[] EyeSubRules =
        {
            new SubCategoryRule(
                BlendShapeSubCategory.EyeLid,
                NoPatterns,
                new[] { "blink", "eyelid", "wink", "まばたき", "瞬き", "まぶた", "瞼", "閉じ", "ウィンク", "ウインク", "ｳｨﾝｸ" },
                new[] { "lid", "lids", "close", "closed" }),
            new SubCategoryRule(
                BlendShapeSubCategory.EyeIris,
                NoPatterns,
                new[] { "iris", "pupil", "highlight", "瞳", "ハイライト", "虹彩" },
                NoPatterns),
            new SubCategoryRule(
                BlendShapeSubCategory.EyeLash,
                NoPatterns,
                new[] { "lash", "まつ毛", "まつげ", "睫" },
                NoPatterns),
            new SubCategoryRule(
                BlendShapeSubCategory.EyeGaze,
                NoPatterns,
                new[] { "gaze", "視線", "目線" },
                new[] { "look" }),
        };

        private static readonly SubCategoryRule[] MouthSubRules =
        {
            new SubCategoryRule(
                BlendShapeSubCategory.MouthViseme,
                new[] { "あ", "い", "う", "え", "お", "ワ", "ω", "▲", "∧" },
                new[] { "viseme", "vrc.v_", "lipsync" },
                NoPatterns),
            new SubCategoryRule(
                BlendShapeSubCategory.MouthLip,
                NoPatterns,
                new[] { "唇", "リップ" },
                new[] { "lip", "lips" }),
            new SubCategoryRule(
                BlendShapeSubCategory.MouthCorner,
                NoPatterns,
                new[] { "口角", "corner", "smile", "frown", "grin", "にやり", "にっこり" },
                NoPatterns),
        };

        private static readonly BlendShapeSubCategory[] EyeSubOrder =
        {
            BlendShapeSubCategory.EyeLid,
            BlendShapeSubCategory.EyeIris,
            BlendShapeSubCategory.EyeLash,
            BlendShapeSubCategory.EyeGaze,
            BlendShapeSubCategory.EyeShape,
        };

        private static readonly BlendShapeSubCategory[] MouthSubOrder =
        {
            BlendShapeSubCategory.MouthViseme,
            BlendShapeSubCategory.MouthLip,
            BlendShapeSubCategory.MouthCorner,
            BlendShapeSubCategory.MouthShape,
        };

        private static readonly BlendShapeSubCategory[] NoSubCategories = new BlendShapeSubCategory[0];

        private static readonly int SubCategoryValueCount =
            System.Enum.GetValues(typeof(BlendShapeSubCategory)).Length;

        /// <summary>Number of sub category values, for count arrays indexed by the enum.</summary>
        public static int SubCategoryCount
        {
            get { return SubCategoryValueCount; }
        }

        /// <summary>
        /// Sub parts of a face part, in display order. Empty for parts that are not split.
        /// </summary>
        public static BlendShapeSubCategory[] GetSubCategories(BlendShapeCategory category)
        {
            switch (category)
            {
                case BlendShapeCategory.Eye: return EyeSubOrder;
                case BlendShapeCategory.Mouth: return MouthSubOrder;
                default: return NoSubCategories;
            }
        }

        public static BlendShapeCategory GetParent(BlendShapeSubCategory subCategory)
        {
            switch (subCategory)
            {
                case BlendShapeSubCategory.EyeLid:
                case BlendShapeSubCategory.EyeIris:
                case BlendShapeSubCategory.EyeLash:
                case BlendShapeSubCategory.EyeGaze:
                case BlendShapeSubCategory.EyeShape:
                    return BlendShapeCategory.Eye;
                case BlendShapeSubCategory.MouthViseme:
                case BlendShapeSubCategory.MouthLip:
                case BlendShapeSubCategory.MouthCorner:
                case BlendShapeSubCategory.MouthShape:
                    return BlendShapeCategory.Mouth;
                default:
                    return BlendShapeCategory.Other;
            }
        }

        public static string GetSubLabel(BlendShapeSubCategory subCategory)
        {
            switch (subCategory)
            {
                case BlendShapeSubCategory.EyeLid: return "Eyelid";
                case BlendShapeSubCategory.EyeIris: return "Iris";
                case BlendShapeSubCategory.EyeLash: return "Lash";
                case BlendShapeSubCategory.EyeGaze: return "Gaze";
                case BlendShapeSubCategory.EyeShape: return "Shape";
                case BlendShapeSubCategory.MouthViseme: return "Viseme";
                case BlendShapeSubCategory.MouthLip: return "Lip";
                case BlendShapeSubCategory.MouthCorner: return "Corner";
                case BlendShapeSubCategory.MouthShape: return "Shape";
                default: return "";
            }
        }

        /// <summary>
        /// Returns the sub part inside an already decided face part. Parts that are not split
        /// return None, and anything the rules do not recognise falls into the catch all of
        /// that parent, so no BlendShape disappears from its group.
        /// </summary>
        public static BlendShapeSubCategory ClassifySub(BlendShapeCategory category, string name)
        {
            SubCategoryRule[] rules;
            BlendShapeSubCategory fallback;

            switch (category)
            {
                case BlendShapeCategory.Eye:
                    rules = EyeSubRules;
                    fallback = BlendShapeSubCategory.EyeShape;
                    break;
                case BlendShapeCategory.Mouth:
                    rules = MouthSubRules;
                    fallback = BlendShapeSubCategory.MouthShape;
                    break;
                default:
                    return BlendShapeSubCategory.None;
            }

            if (string.IsNullOrEmpty(name))
            {
                return fallback;
            }

            string trimmed = name.Trim();
            string lower = trimmed.ToLowerInvariant();
            Tokenize(trimmed, TokenBuffer);

            for (int i = 0; i < rules.Length; i++)
            {
                SubCategoryRule rule = rules[i];

                if (MatchesExact(trimmed, rule.ExactNames)
                    || MatchesKeyword(lower, rule.Keywords)
                    || MatchesToken(TokenBuffer, rule.Tokens))
                {
                    return rule.SubCategory;
                }
            }

            return fallback;
        }

        private static readonly List<string> TokenBuffer = new List<string>();
        private static readonly StringBuilder TokenBuilder = new StringBuilder();

        public static string GetLabel(BlendShapeCategory category)
        {
            switch (category)
            {
                case BlendShapeCategory.Eye: return "Eye";
                case BlendShapeCategory.Brow: return "Brow";
                case BlendShapeCategory.Mouth: return "Mouth";
                case BlendShapeCategory.Tongue: return "Tongue";
                case BlendShapeCategory.Teeth: return "Teeth";
                case BlendShapeCategory.Nose: return "Nose";
                case BlendShapeCategory.Cheek: return "Cheek";
                case BlendShapeCategory.Jaw: return "Jaw";
                case BlendShapeCategory.Ear: return "Ear";
                default: return "Other";
            }
        }

        /// <summary>
        /// Returns the face part the name points at, or Other when nothing matches.
        /// Called when the BlendShape list is built, not every frame.
        /// </summary>
        public static BlendShapeCategory Classify(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return BlendShapeCategory.Other;
            }

            string trimmed = name.Trim();
            string lower = trimmed.ToLowerInvariant();
            Tokenize(trimmed, TokenBuffer);

            for (int i = 0; i < Rules.Length; i++)
            {
                CategoryRule rule = Rules[i];

                if (MatchesExact(trimmed, rule.ExactNames)
                    || MatchesKeyword(lower, rule.Keywords)
                    || MatchesToken(TokenBuffer, rule.Tokens))
                {
                    return rule.Category;
                }
            }

            return BlendShapeCategory.Other;
        }

        private static bool MatchesExact(string name, string[] exactNames)
        {
            for (int i = 0; i < exactNames.Length; i++)
            {
                if (string.Equals(name, exactNames[i], System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesKeyword(string lowerName, string[] keywords)
        {
            for (int i = 0; i < keywords.Length; i++)
            {
                if (lowerName.IndexOf(keywords[i], System.StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesToken(List<string> tokens, string[] wanted)
        {
            for (int i = 0; i < wanted.Length; i++)
            {
                for (int t = 0; t < tokens.Count; t++)
                {
                    if (string.Equals(tokens[t], wanted[i], System.StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Splits a name into lowercase words, breaking on anything that is not an ASCII
        /// letter or digit and on camelCase boundaries, so both "Eye_Blink_L" and
        /// "eyeBlinkLeft" produce the same words.
        /// </summary>
        private static void Tokenize(string name, List<string> results)
        {
            results.Clear();
            TokenBuilder.Length = 0;

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool isLetter = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
                bool isDigit = c >= '0' && c <= '9';

                if (!isLetter && !isDigit)
                {
                    FlushToken(results);
                    continue;
                }

                if (isLetter && c >= 'A' && c <= 'Z' && TokenBuilder.Length > 0)
                {
                    char previous = name[i - 1];
                    bool previousWasLowerOrDigit =
                        (previous >= 'a' && previous <= 'z') || (previous >= '0' && previous <= '9');
                    if (previousWasLowerOrDigit)
                    {
                        FlushToken(results);
                    }
                }

                TokenBuilder.Append(char.ToLowerInvariant(c));
            }

            FlushToken(results);
        }

        private static void FlushToken(List<string> results)
        {
            if (TokenBuilder.Length > 0)
            {
                results.Add(TokenBuilder.ToString());
                TokenBuilder.Length = 0;
            }
        }
    }
}
