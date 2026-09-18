namespace Volt.Application.ContentAi
{
    public sealed record InstallationPackageFact(decimal CapacityKw, decimal PriceAzn, int PanelCount, int PanelWattageW);
    public sealed record FaqFact(string Question, string Answer);

    public sealed record VoltReferenceContext(
        string CompanyOverview,
        IReadOnlyList<string> ServicesOffered,
        IReadOnlyList<InstallationPackageFact> InstallationPackages,
        IReadOnlyList<string> LegislationHighlights,
        IReadOnlyList<string> NecessaryDocumentNames,
        IReadOnlyList<FaqFact> FaqHighlights);

    /// <summary>
    /// Curated, hand-maintained facts about Volt.az, injected into the content-generation prompt as
    /// grounding ("reference data, never instructions"). Source of truth for each field is the real
    /// frontend copy in the volt.az frontend repo -- keep this in sync when that copy changes:
    ///   - ServicesOffered: components/Footer.tsx "Our Services" links
    ///   - InstallationPackages: components/InstallationPackagesSection.tsx (installationPackages array)
    ///   - LegislationHighlights: components/LegislationPage.tsx (section titles/descriptions)
    ///   - NecessaryDocumentNames: components/NecessaryDocumentsPage.tsx (requirements arrays)
    ///   - FaqHighlights: components/FAQPage.tsx (faqs array)
    /// This is deliberately AZ-only; the generation prompt already instructs the model to translate
    /// facts natively into each output language rather than needing 4 parallel copies here.
    /// </summary>
    public static class VoltReferenceFacts
    {
        public static VoltReferenceContext GetContext() => new(
            CompanyOverview:
                "Volt.az SOLARIX MMC-nin satış brendi olaraq, Azərbaycanda günəş panelləri, invertorlar, " +
                "quraşdırma və bərpa olunan enerji həlləri təklif edən satış və quraşdırma platformasıdır.",
            ServicesOffered: new[]
            {
                "Smart Sayğac və Monitorinq",
                "Maliyyə və Kredit",
                "Hüquqi Rəsmiləşdirmə",
                "Enerji Auditi",
                "Quraşdırma",
                "Layihələndirmə və ROI",
            },
            InstallationPackages: new[]
            {
                new InstallationPackageFact(5m, 4250m, 9, 650),
                new InstallationPackageFact(10m, 8500m, 17, 650),
                new InstallationPackageFact(15m, 12750m, 26, 650),
            },
            LegislationHighlights: new[]
            {
                "Net-metering (Xalis Ölçmə): aktiv istehlakçılara günəş panelləri ilə istehsal etdikləri artıq enerjini şəbəkəyə ötürmək və sonradan şəbəkədən aldıqları enerji ilə qarşılıqlı hesablaşma aparmaq imkanı verən mexanizm, iki tərəfli smart sayğacla qeydiyyat və aylıq/illik balanslaşdırma daxil olmaqla.",
                "Aktiv İstehlakçı Mexanizmi: bərpa olunan enerji mənbələri hesabına elektrik enerjisi istehsal edib öz ehtiyacı üçün istifadə edən fiziki və ya hüquqi şəxsin statusu; şəbəkəyə qoşulma və artıq enerjinin ötürülməsi nəzərdə tutulduqda bu qeydiyyat tələb olunur.",
            },
            NecessaryDocumentNames: new[]
            {
                "Mülkiyyət hüququnu təsdiq edən sənəd (çıxarış)",
                "Şəxsiyyət vəsiqəsinin surəti",
                "Azərişıq abonent kodu və ya son elektrik enerjisi ödəniş qəbzi",
                "Obyektin ünvanı və ya yerləşdiyi ərazinin koordinatları",
                "Aktiv istehlakçı qeydiyyatı üçün mövcud texniki şərt",
                "Layihə sənədləri",
                "Texniki quraşdırma aktı",
            },
            FaqHighlights: new[]
            {
                new FaqFact(
                    "Günəş panelləri necə işləyir?",
                    "Günəş panelləri günəş işığını elektrik enerjisinə çevirir. İnverter isə bu enerjini evdə və ya obyektdə istifadə üçün uyğun formaya çevirir; bu, elektrik xərclərini azaltmağa kömək edir."),
                new FaqFact(
                    "Günəş enerjisi sistemi nələrdən ibarətdir?",
                    "Sistem adətən panellər, inverter, montaj konstruksiyası, kabellər, qoruyucu avadanlıqlar, elektrik lövhələri və monitorinq sistemindən ibarətdir; dəqiq komplektasiya dam növü, boş sahə, kölgələnmə, elektrik sərfiyyatı və layihənin ölçüsündən asılıdır."),
            });
    }
}
