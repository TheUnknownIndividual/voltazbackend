using System.Globalization;

namespace Volt.Application.ContentAi
{
    public sealed record EducationTopic(int Id, string Key, string Topic, IReadOnlyList<string> Facts, string Path);

    /// <summary>
    /// Verified question-and-answer material for educational social posts. Every fact is taken word for
    /// word from copy Volt already publishes, so the AI can only rephrase what is approved. Keep in sync with:
    ///   - components/FAQPage.tsx (faqs array): topics 1-8
    ///   - VoltReferenceFacts (legislation highlights, installation packages, necessary documents): topics 9-12
    /// Topic ids are stable (they feed the SocialPosts idempotency key); never renumber, only append.
    /// </summary>
    public static class SocialEducationBank
    {
        public static IReadOnlyList<EducationTopic> GetTopics()
        {
            var reference = VoltReferenceFacts.GetContext();

            var packageFacts = reference.InstallationPackages
                .Select(p => $"{p.CapacityKw.ToString("0.##", CultureInfo.InvariantCulture)} kW quraşdırma paketi: " +
                             $"{p.PanelCount} ədəd {p.PanelWattageW} W günəş paneli, qiyməti {p.PriceAzn.ToString("0.##", CultureInfo.InvariantCulture)} AZN.")
                .ToList();

            return new List<EducationTopic>
            {
                new(1, "how-panels-work", "Günəş panelləri necə işləyir?", new[]
                {
                    "Günəş panelləri günəş işığını elektrik enerjisinə çevirir. İnverter isə bu enerjini evdə və ya obyektdə istifadə üçün uyğun formaya çevirir; bu, elektrik xərclərini azaltmağa və təmiz enerjidən daha səmərəli istifadə etməyə kömək edir.",
                }, "/faq"),
                new(2, "system-components", "Günəş enerjisi sistemi nələrdən ibarətdir?", new[]
                {
                    "Günəş enerji sistemi adətən panellər, inverter, montaj konstruksiyası, kabellər, qoruyucu avadanlıqlar, elektrik lövhələri və monitorinq sistemindən ibarət olur.",
                    "Dəqiq komplektasiya dam növü, boş sahə, kölgələnmə, elektrik sərfiyyatı, qoşulma tipi və layihənin ölçüsündən asılıdır.",
                }, "/faq"),
                new(3, "cost-per-kw", "1 kW günəş sistemi neçəyə başa gəlir?", new[]
                {
                    "Ümumi hesabla 1 kW günəş enerji sisteminin qiyməti təxminən 1000 AZN-dən başlaya bilər.",
                    "Yekun qiymət panel və inverter markası, dam növü, montajın çətinliyi, kabel məsafəsi, elektrik lövhəsinin vəziyyəti, sənədləşmə tələbləri, sistemin gücü və quraşdırma şəraitindən asılıdır.",
                    "Qiyməti öz obyektiniz üçün təxmini hesablamaq üçün saytdakı günəş kalkulyatorundan istifadə edə bilərsiniz.",
                }, "/calculator"),
                new(4, "system-size", "Mənə neçə kW sistem lazımdır?", new[]
                {
                    "Lazım olan sistem gücü aylıq elektrik sərfiyyatınızdan, dam sahəsindən, günəşlənmə şəraitindən, büdcənizdən və elektrik xərclərini qismən, yoxsa maksimum azaltmaq istəyinizdən asılıdır.",
                    "Solarix sizin sərfiyyatınızı və obyekt şəraitini yoxladıqdan sonra uyğun sistemi təklif edir.",
                }, "/calculator"),
                new(5, "installation-time", "Günəş sisteminin quraşdırılması nə qədər vaxt aparır?", new[]
                {
                    "Əksər fərdi yaşayış evlərində quraşdırma adətən 1–3 gün çəkir.",
                    "Sistemin ölçüsü, damın vəziyyəti və layihənin mürəkkəbliyindən asılı olaraq bu müddət dəyişə bilər.",
                }, "/solar-installation"),
                new(6, "export-to-grid", "Günəş panelinin artıq enerjisini şəbəkəyə ötürmək olarmı?", new[]
                {
                    "Bəli. Sistem rəsmi qaydada sənədləşdirildikdə və aktiv istehlakçı mexanizmi üzrə qoşulduqda artıq istehsal olunan enerji şəbəkəyə ötürülə və ikitərəfli sayğac vasitəsilə qeydiyyata alına bilər.",
                }, "/legislation"),
                new(7, "documentation-help", "Günəş sistemi üçün sənədləşməni kim edir?", new[]
                {
                    "Solarix texniki sənədlər, layihə məlumatları, quraşdırma sənədləri və zəruri hallarda aktiv istehlakçı müraciəti prosesində müştəriyə dəstək göstərir.",
                }, "/necessary-documents"),
                new(8, "phone-monitoring", "Günəş sistemini telefondan izləmək mümkündürmü?", new[]
                {
                    "Bəli. Sistem aktivləşdirildikdən sonra sizə monitorinq tətbiqinə giriş verilə bilər.",
                    "Bu tətbiq vasitəsilə enerji istehsalını, sərfiyyatı, şəbəkədən alınan enerjini, şəbəkəyə ötürülən enerjini, inverterin vəziyyətini və ümumi sistem performansını izləmək mümkündür.",
                }, "/faq"),
                new(9, "active-consumer", "Aktiv istehlakçı kimdir?", new[]
                {
                    reference.LegislationHighlights[1],
                }, "/legislation"),
                new(10, "net-metering", "Net-metering (xalis ölçmə) nədir?", new[]
                {
                    reference.LegislationHighlights[0],
                }, "/legislation"),
                new(11, "installation-packages", "Hazır günəş paketlərinin qiyməti nə qədərdir?", packageFacts, "/solar-installation"),
                new(12, "necessary-documents", "Günəş sistemi üçün hansı sənədlər lazımdır?", new[]
                {
                    "Aşağıdakı sənədlər tələb olunur: " + string.Join("; ", reference.NecessaryDocumentNames) + ".",
                }, "/necessary-documents"),
            };
        }
    }
}
