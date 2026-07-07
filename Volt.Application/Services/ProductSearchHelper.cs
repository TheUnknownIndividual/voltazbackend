using System.Globalization;
using System.Text;

namespace Volt.Application.Services
{
    internal static class ProductSearchHelper
    {
        private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
        {
            "a",
            "an",
            "and",
            "the",
            "for",
            "with",
            "of",
            "to",
            "in",
            "on",
            "or",
            "ve",
            "və",
            "ile",
            "ucun",
            "üçün",
            "для",
            "или",
            "по"
        };

        private static readonly Dictionary<string, string[]> Synonyms = new(StringComparer.Ordinal)
        {
            ["cable"] = ["cable", "kabel", "naqil", "kablo", "кабель", "кабельный", "провод"],
            ["kabel"] = ["cable", "kabel", "naqil", "kablo", "кабель", "кабельный", "провод"],
            ["naqil"] = ["cable", "kabel", "naqil", "kablo", "кабель", "кабельный", "провод"],
            ["kablo"] = ["cable", "kabel", "naqil", "kablo", "кабель", "кабельный", "провод"],
            ["кабель"] = ["cable", "kabel", "naqil", "kablo", "кабель", "кабельный", "провод"],
            ["кабельный"] = ["cable", "kabel", "naqil", "kablo", "кабель", "кабельный", "провод"],
            ["провод"] = ["cable", "kabel", "naqil", "kablo", "кабель", "кабельный", "провод"],
            ["stainless"] = ["stainless", "paslanmayan", "paslanmaz", "нержавеющий", "нержавеющая", "нержавеющей", "нержавеющие"],
            ["paslanmayan"] = ["stainless", "paslanmayan", "paslanmaz", "нержавеющий", "нержавеющая", "нержавеющей", "нержавеющие"],
            ["paslanmaz"] = ["stainless", "paslanmayan", "paslanmaz", "нержавеющий", "нержавеющая", "нержавеющей", "нержавеющие"],
            ["нержавеющий"] = ["stainless", "paslanmayan", "paslanmaz", "нержавеющий", "нержавеющая", "нержавеющей", "нержавеющие"],
            ["нержавеющая"] = ["stainless", "paslanmayan", "paslanmaz", "нержавеющий", "нержавеющая", "нержавеющей", "нержавеющие"],
            ["нержавеющей"] = ["stainless", "paslanmayan", "paslanmaz", "нержавеющий", "нержавеющая", "нержавеющей", "нержавеющие"],
            ["нержавеющие"] = ["stainless", "paslanmayan", "paslanmaz", "нержавеющий", "нержавеющая", "нержавеющей", "нержавеющие"],
            ["steel"] = ["steel", "polad", "celik", "çelik", "сталь", "стали", "стальной", "стальная"],
            ["polad"] = ["steel", "polad", "celik", "çelik", "сталь", "стали", "стальной", "стальная"],
            ["celik"] = ["steel", "polad", "celik", "çelik", "сталь", "стали", "стальной", "стальная"],
            ["çelik"] = ["steel", "polad", "celik", "çelik", "сталь", "стали", "стальной", "стальная"],
            ["сталь"] = ["steel", "polad", "celik", "çelik", "сталь", "стали", "стальной", "стальная"],
            ["стали"] = ["steel", "polad", "celik", "çelik", "сталь", "стали", "стальной", "стальная"],
            ["стальной"] = ["steel", "polad", "celik", "çelik", "сталь", "стали", "стальной", "стальная"],
            ["стальная"] = ["steel", "polad", "celik", "çelik", "сталь", "стали", "стальной", "стальная"],
            ["connector"] = ["connector", "konnektor", "baglayici", "bağlayıcı", "коннектор", "соединитель", "разъем", "разъём"],
            ["connectors"] = ["connector", "konnektor", "baglayici", "bağlayıcı", "коннектор", "соединитель", "разъем", "разъём"],
            ["konnektor"] = ["connector", "konnektor", "baglayici", "bağlayıcı", "коннектор", "соединитель", "разъем", "разъём"],
            ["baglayici"] = ["connector", "konnektor", "baglayici", "bağlayıcı", "коннектор", "соединитель", "разъем", "разъём"],
            ["bağlayıcı"] = ["connector", "konnektor", "baglayici", "bağlayıcı", "коннектор", "соединитель", "разъем", "разъём"],
            ["коннектор"] = ["connector", "konnektor", "baglayici", "bağlayıcı", "коннектор", "соединитель", "разъем", "разъём"],
            ["соединитель"] = ["connector", "konnektor", "baglayici", "bağlayıcı", "коннектор", "соединитель", "разъем", "разъём"],
            ["разъем"] = ["connector", "konnektor", "baglayici", "bağlayıcı", "коннектор", "соединитель", "разъем", "разъём"],
            ["разъём"] = ["connector", "konnektor", "baglayici", "bağlayıcı", "коннектор", "соединитель", "разъем", "разъём"],
            ["gland"] = ["gland", "rakor", "ввод", "сальник"],
            ["rakor"] = ["gland", "rakor", "ввод", "сальник"],
            ["ввод"] = ["gland", "rakor", "ввод", "сальник"],
            ["сальник"] = ["gland", "rakor", "ввод", "сальник"],
            ["inverter"] = ["inverter", "invertor", "инвертор"],
            ["invertor"] = ["inverter", "invertor", "инвертор"],
            ["инвертор"] = ["inverter", "invertor", "инвертор"],
            ["panel"] = ["panel", "панель", "панели"],
            ["панель"] = ["panel", "панель", "панели"],
            ["панели"] = ["panel", "панель", "панели"],
            ["battery"] = ["battery", "batareya", "akkumulyator", "батарея", "аккумулятор"],
            ["batareya"] = ["battery", "batareya", "akkumulyator", "батарея", "аккумулятор"],
            ["akkumulyator"] = ["battery", "batareya", "akkumulyator", "батарея", "аккумулятор"],
            ["батарея"] = ["battery", "batareya", "akkumulyator", "батарея", "аккумулятор"],
            ["аккумулятор"] = ["battery", "batareya", "akkumulyator", "батарея", "аккумулятор"],
            ["storage"] = ["storage", "saxlama", "depolama", "хранение", "накопитель"],
            ["saxlama"] = ["storage", "saxlama", "depolama", "хранение", "накопитель"],
            ["depolama"] = ["storage", "saxlama", "depolama", "хранение", "накопитель"],
            ["хранение"] = ["storage", "saxlama", "depolama", "хранение", "накопитель"],
            ["накопитель"] = ["storage", "saxlama", "depolama", "хранение", "накопитель"],
            ["solar"] = ["solar", "sun", "gunes", "günəş", "güneş", "солнечный", "солнечная", "солнце"],
            ["gunes"] = ["solar", "sun", "gunes", "günəş", "güneş", "солнечный", "солнечная", "солнце"],
            ["günəş"] = ["solar", "sun", "gunes", "günəş", "güneş", "солнечный", "солнечная", "солнце"],
            ["güneş"] = ["solar", "sun", "gunes", "günəş", "güneş", "солнечный", "солнечная", "солнце"],
            ["солнечный"] = ["solar", "sun", "gunes", "günəş", "güneş", "солнечный", "солнечная", "солнце"],
            ["солнечная"] = ["solar", "sun", "gunes", "günəş", "güneş", "солнечный", "солнечная", "солнце"],
            ["солнце"] = ["solar", "sun", "gunes", "günəş", "güneş", "солнечный", "солнечная", "солнце"]
        };

        public static string Normalize(string value)
            => (value ?? string.Empty).Trim().ToLowerInvariant();

        public static bool ContainsQuery(string value, string query)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(query))
            {
                return false;
            }

            if (Normalize(value).Contains(query))
            {
                return true;
            }

            var searchableValue = NormalizeForSearch(value);
            var searchableQuery = NormalizeForSearch(query);
            if (!string.IsNullOrWhiteSpace(searchableQuery) && searchableValue.Contains(searchableQuery))
            {
                return true;
            }

            var searchQuery = CreateQuery(query);
            if (searchQuery.Terms.Count == 0)
            {
                return false;
            }

            var valueTokens = GetSearchTokens(value);
            if (valueTokens.Count == 0)
            {
                return false;
            }

            return searchQuery.Terms.All(term =>
                valueTokens.Any(valueToken => term.Tokens.Any(queryToken => IsTokenMatch(valueToken, queryToken))));
        }

        public static string FirstSearchToken(string query)
            => GetSearchTokens(query).FirstOrDefault() ?? Normalize(query);

        public static ProductSearchQuery CreateQuery(string value)
        {
            var terms = GetSearchTokens(value)
                .Select((token, index) => new ProductSearchTerm(index, token, ExpandToken(token)))
                .GroupBy(x => x.Token)
                .Select(g => g.First())
                .ToList();

            return new ProductSearchQuery(
                value ?? string.Empty,
                string.Join(' ', terms.Select(x => x.Token)),
                terms,
                ParseDecimal(Normalize(value)));
        }

        public static ProductSearchTextScore ScoreText(
            string value,
            ProductSearchQuery query,
            double phraseWeight,
            double allTermsWeight,
            double termWeight)
        {
            if (string.IsNullOrWhiteSpace(value) || query.Terms.Count == 0)
            {
                return ProductSearchTextScore.Empty;
            }

            var searchableValue = NormalizeForSearch(value);
            if (string.IsNullOrWhiteSpace(searchableValue))
            {
                return ProductSearchTextScore.Empty;
            }

            var valueTokens = GetSearchTokens(value);
            var valueTokenPhrase = string.Join(' ', valueTokens);
            if (valueTokens.Count == 0)
            {
                return ProductSearchTextScore.Empty;
            }

            var matchedIndexes = new HashSet<int>();
            foreach (var term in query.Terms)
            {
                if (valueTokens.Any(valueToken => term.Tokens.Any(queryToken => IsTokenMatch(valueToken, queryToken))))
                {
                    matchedIndexes.Add(term.Index);
                }
            }

            if (matchedIndexes.Count == 0)
            {
                return ProductSearchTextScore.Empty;
            }

            var score = matchedIndexes.Count * termWeight;
            var phraseMatch = query.Terms.Count > 1
                && !string.IsNullOrWhiteSpace(query.NormalizedPhrase)
                && (searchableValue.Contains(query.NormalizedPhrase)
                    || valueTokenPhrase.Contains(query.NormalizedPhrase));
            if (phraseMatch)
            {
                score += phraseWeight;
            }

            if (matchedIndexes.Count == query.Terms.Count && query.Terms.Count > 1)
            {
                score += allTermsWeight;
            }

            return new ProductSearchTextScore(score, matchedIndexes, phraseMatch);
        }

        public static decimal? ParseDecimal(string query)
        {
            if (decimal.TryParse(query, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantValue))
            {
                return invariantValue;
            }

            if (decimal.TryParse(query.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var normalizedValue))
            {
                return normalizedValue;
            }

            return null;
        }

        public static string NormalizeForSearch(string value)
        {
            var builder = new StringBuilder();
            foreach (var ch in Normalize(value))
            {
                builder.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
            }

            return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        public static IReadOnlyList<string> GetSearchTokens(string value)
            => NormalizeForSearch(value)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeToken)
                .Where(x => (x.Length > 1 || x.All(char.IsDigit)) && !StopWords.Contains(x))
                .Distinct()
                .ToList();

        public static string NormalizeTokenPhrase(string value)
            => string.Join(' ', GetSearchTokens(value));

        private static string NormalizeToken(string token)
            => token.Length > 3 && token.EndsWith('s') && token.All(char.IsLetter)
                ? token[..^1]
                : token;

        private static IReadOnlyList<string> ExpandToken(string token)
            => Synonyms.TryGetValue(token, out var synonyms)
                ? synonyms.Select(NormalizeToken).Distinct().ToList()
                : [token];

        private static bool IsTokenMatch(string valueToken, string queryToken)
            => valueToken == queryToken
                || (queryToken.Length > 2 && valueToken.Contains(queryToken))
                || (valueToken.Length > 2 && queryToken.Contains(valueToken));
    }

    internal sealed record ProductSearchQuery(
        string Raw,
        string NormalizedPhrase,
        IReadOnlyList<ProductSearchTerm> Terms,
        decimal? NumericQuery)
    {
        public bool IsSingleTerm => Terms.Count == 1;
    }

    internal sealed record ProductSearchTerm(
        int Index,
        string Token,
        IReadOnlyList<string> Tokens);

    internal sealed record ProductSearchTextScore(
        double Score,
        IReadOnlySet<int> MatchedTermIndexes,
        bool PhraseMatch)
    {
        public static ProductSearchTextScore Empty { get; } = new(0, new HashSet<int>(), false);
    }
}
