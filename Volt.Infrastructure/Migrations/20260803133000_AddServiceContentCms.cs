using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

[DbContext(typeof(DataContext))]
[Migration("20260803133000_AddServiceContentCms")]
public partial class AddServiceContentCms : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "BannerImageUrl", table: "ServiceManagements", type: "nvarchar(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<int>(name: "Category", table: "ServiceManagements", type: "int", nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<string>(name: "DetailPageSlug", table: "ServiceManagements", type: "nvarchar(160)", maxLength: 160, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ReadMoreUrl", table: "ServiceManagements", type: "nvarchar(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<string>(name: "DetailContentHtml", table: "ServiceManagementLanguages", type: "nvarchar(max)", nullable: true);

        migrationBuilder.CreateTable(
            name: "ServiceCategorySettings",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                Category = table.Column<int>(type: "int", nullable: false),
                IsReadMoreEnabled = table.Column<bool>(type: "bit", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ServiceCategorySettings", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_ServiceManagements_DetailPageSlug", table: "ServiceManagements", column: "DetailPageSlug", unique: true, filter: "[DetailPageSlug] IS NOT NULL");
        migrationBuilder.CreateIndex(name: "IX_ServiceCategorySettings_Category", table: "ServiceCategorySettings", column: "Category", unique: true);

        migrationBuilder.Sql("""
            INSERT INTO [ServiceCategorySettings] ([Category], [IsReadMoreEnabled], [UpdatedAt])
            VALUES (1, 0, SYSUTCDATETIME()), (2, 1, SYSUTCDATETIME());

            DECLARE @CorporateServices TABLE (
                    [TitleAz] nvarchar(150), [DescriptionAz] nvarchar(max),
                    [Content1Az] nvarchar(max), [Content2Az] nvarchar(max), [Content3Az] nvarchar(max), [Content4Az] nvarchar(max),
                    [TitleEn] nvarchar(150), [DescriptionEn] nvarchar(max),
                    [Content1En] nvarchar(max), [Content2En] nvarchar(max), [Content3En] nvarchar(max), [Content4En] nvarchar(max),
                    [Icon] nvarchar(100), [Slug] nvarchar(160));

                INSERT INTO @CorporateServices VALUES
                (N'Topdan Satış və Konteyner Təchizatı', N'Solarix korporativ müştərilər və iri layihələr üçün günəş enerjisi avadanlıqlarının topdan satışını həyata keçirir.', N'Günəş panelləri, inverterlər və enerji saxlama sistemləri', N'Tam konteyner — FCL və iri həcmli təchizat', N'Layihəyə uyğun qarışıq məhsul partiyalarının hazırlanması', N'Böyük sifarişlər üçün xüsusi korporativ qiymətlər', N'Wholesale and Container Supply', N'Solarix supplies solar energy equipment wholesale for corporate customers and large-scale projects.', N'Solar panels, inverters, and energy storage systems', N'Full-container (FCL) and high-volume supply', N'Mixed product batches prepared to project requirements', N'Special corporate pricing for large orders', N'Parametrlər', N'wholesale-container-supply'),
                (N'LONGi-nin Azərbaycanda Yeganə Rəsmi Tərəfdaşı', N'Solarix Azərbaycanda LONGi məhsullarının yeganə rəsmi tərəfdaşı olaraq orijinal və sertifikatlaşdırılmış günəş panelləri təqdim edir.', N'Orijinal və sertifikatlaşdırılmış LONGi məhsulları', N'İstehsalçı zəmanəti və mənşə sənədləri', N'Məhsulun autentikliyinin və seriya nömrəsinin yoxlanılması', N'Rəsmi satış, texniki və zəmanət dəstəyi', N'LONGi''s Only Official Partner in Azerbaijan', N'As the only official LONGi partner in Azerbaijan, Solarix provides genuine and certified solar panels.', N'Genuine and certified LONGi products', N'Manufacturer warranty and certificates of origin', N'Product authenticity and serial-number verification', N'Official sales, technical, and warranty support', N'Konsultasiya', N'longi-official-partner'),
                (N'Açar Təhvil Günəş Enerjisi Layihələri', N'Solarix layihələndirmə, avadanlıq təchizatı, quraşdırma və istismara vermə daxil olmaqla tam EPC xidmətləri göstərir.', N'Obyektə baxış və texniki qiymətləndirmə', N'Günəş panelləri və montaj konstruksiyalarının quraşdırılması', N'İnverter, kabel və elektrik avadanlıqlarının montajı', N'Sistemin sınaqdan keçirilməsi və istismara verilməsi', N'Turnkey Solar Energy Projects', N'Solarix provides complete EPC services, including design, equipment supply, installation, and commissioning.', N'Site survey and technical assessment', N'Installation of solar panels and mounting structures', N'Installation of inverters, cabling, and electrical equipment', N'System testing and commissioning', N'Günəş', N'turnkey-solar-projects'),
                (N'Layihələndirmə və ROI Analizi', N'Korporativ obyektlər üçün optimal sistem gücü, enerji istehsalı və investisiyanın geri dönüşü hesablanır.', N'Elektrik sərfiyyatına uyğun sistem gücünün hesablanması', N'İllik enerji istehsalı və qənaət proqnozu', N'İnvestisiyanın geri dönüş müddəti və ROI analizi', N'Avadanlıq tərkibinin və layihə uyğunluğunun müəyyən edilməsi', N'Design and ROI Analysis', N'We calculate optimal system capacity, energy generation, and investment payback for corporate facilities.', N'System sizing based on electricity consumption', N'Annual energy generation and savings forecast', N'Payback period and ROI analysis', N'Equipment selection and project suitability assessment', N'Maliyyə', N'design-roi-analysis'),
                (N'Logistika və Korporativ Təchizat', N'İri həcmli sifarişlərin istehsalçıdan layihə ünvanına qədər çatdırılması və təchizat prosesi idarə olunur.', N'Konteyner və beynəlxalq yükdaşımaların koordinasiyası', N'Gömrük və idxal sənədlərinin hazırlanmasına dəstək', N'Mənşə və uyğunluq sertifikatlarının təqdim edilməsi', N'Mərhələli çatdırılma və anbarlama imkanları', N'Logistics and Corporate Supply', N'We manage delivery and supply of high-volume orders from the manufacturer to the project site.', N'Container and international freight coordination', N'Support with customs and import documentation', N'Certificates of origin and conformity', N'Phased delivery and warehousing options', N'Sayğac', N'logistics-corporate-supply'),
                (N'Texniki Xidmət və Satışdan Sonrakı Dəstək', N'Günəş sistemlərinin uzunmüddətli, təhlükəsiz və yüksək məhsuldarlıqla işləməsi üçün tam texniki dəstək təqdim edilir.', N'Planlı texniki baxış və sistem diaqnostikası', N'Panel, inverter və elektrik bağlantılarının yoxlanılması', N'Monitorinq və enerji istehsalı göstəricilərinin təhlili', N'Zəmanət və zəmanətdən sonrakı servis dəstəyi', N'Maintenance and After-Sales Support', N'Complete technical support keeps solar systems safe, productive, and reliable over the long term.', N'Scheduled maintenance and system diagnostics', N'Inspection of panels, inverters, and electrical connections', N'Monitoring and energy-generation performance analysis', N'Warranty and post-warranty service support', N'Texniki', N'maintenance-after-sales-support');

                DECLARE corporate_cursor CURSOR LOCAL FAST_FORWARD FOR
                    SELECT [TitleAz], [DescriptionAz], [Content1Az], [Content2Az], [Content3Az], [Content4Az], [TitleEn], [DescriptionEn], [Content1En], [Content2En], [Content3En], [Content4En], [Icon], [Slug]
                    FROM @CorporateServices;
                DECLARE @TitleAz nvarchar(150), @DescriptionAz nvarchar(max), @Content1Az nvarchar(max), @Content2Az nvarchar(max), @Content3Az nvarchar(max), @Content4Az nvarchar(max), @TitleEn nvarchar(150), @DescriptionEn nvarchar(max), @Content1En nvarchar(max), @Content2En nvarchar(max), @Content3En nvarchar(max), @Content4En nvarchar(max), @Icon nvarchar(100), @Slug nvarchar(160), @ServiceId int;
                OPEN corporate_cursor;
                FETCH NEXT FROM corporate_cursor INTO @TitleAz, @DescriptionAz, @Content1Az, @Content2Az, @Content3Az, @Content4Az, @TitleEn, @DescriptionEn, @Content1En, @Content2En, @Content3En, @Content4En, @Icon, @Slug;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [ServiceManagements] WHERE [DetailPageSlug] = @Slug)
                    BEGIN
                        INSERT INTO [ServiceManagements] ([Icon], [IsActive], [Category], [DetailPageSlug], [CreatedAt]) VALUES (@Icon, 1, 2, @Slug, SYSUTCDATETIME());
                        SET @ServiceId = SCOPE_IDENTITY();
                        INSERT INTO [ServiceManagementLanguages] ([ServiceMagamentId], [LanguageCode], [Title], [Description], [Content1], [Content2], [Content3], [Content4], [IsActive])
                        VALUES (@ServiceId, 1, @TitleAz, @DescriptionAz, @Content1Az, @Content2Az, @Content3Az, @Content4Az, 1), (@ServiceId, 2, @TitleEn, @DescriptionEn, @Content1En, @Content2En, @Content3En, @Content4En, 1);
                    END
                    FETCH NEXT FROM corporate_cursor INTO @TitleAz, @DescriptionAz, @Content1Az, @Content2Az, @Content3Az, @Content4Az, @TitleEn, @DescriptionEn, @Content1En, @Content2En, @Content3En, @Content4En, @Icon, @Slug;
                END
                CLOSE corporate_cursor;
                DEALLOCATE corporate_cursor;

                INSERT INTO [ServiceManagementLanguages]
                    ([ServiceMagamentId], [LanguageCode], [Title], [Description], [Content1], [Content2], [Content3], [Content4], [IsActive])
                SELECT service.[Id], translation.[LanguageCode], translation.[Title], translation.[Description],
                       translation.[Content1], translation.[Content2], translation.[Content3], translation.[Content4], 1
                FROM [ServiceManagements] service
                INNER JOIN (VALUES
                    (N'wholesale-container-supply', 3, N'Оптовые продажи и контейнерные поставки', N'Solarix осуществляет оптовые поставки оборудования для солнечной энергетики корпоративным клиентам и крупным проектам.', N'Солнечные панели, инверторы и системы накопления энергии', N'Полные контейнеры — FCL и крупнообъёмные поставки', N'Комплектация смешанных партий под требования проекта', N'Специальные корпоративные цены для крупных заказов'),
                    (N'longi-official-partner', 3, N'Единственный официальный партнёр LONGi в Азербайджане', N'Solarix, являясь единственным официальным партнёром LONGi в Азербайджане, предлагает оригинальные и сертифицированные солнечные панели.', N'Оригинальная и сертифицированная продукция LONGi', N'Гарантия производителя и документы о происхождении', N'Проверка подлинности продукции и серийного номера', N'Официальная техническая, гарантийная и сервисная поддержка'),
                    (N'turnkey-solar-projects', 3, N'Солнечные энергетические проекты под ключ', N'Solarix предоставляет полный комплекс EPC-услуг: проектирование, поставку оборудования, монтаж и ввод системы в эксплуатацию.', N'Обследование объекта и техническая оценка', N'Монтаж солнечных панелей и несущих конструкций', N'Монтаж инверторов, кабелей и электрооборудования', N'Испытание системы и ввод в эксплуатацию'),
                    (N'design-roi-analysis', 3, N'Проектирование и анализ ROI', N'Для корпоративных объектов рассчитываются оптимальная мощность системы, выработка энергии и срок окупаемости инвестиций.', N'Расчёт мощности системы по потреблению электроэнергии', N'Прогноз годовой выработки энергии и экономии', N'Расчёт срока окупаемости и ROI', N'Подбор оборудования и оценка соответствия проекту'),
                    (N'logistics-corporate-supply', 3, N'Логистика и корпоративные поставки', N'Мы управляем доставкой и снабжением крупных заказов от производителя непосредственно до проектного объекта.', N'Координация контейнерных и международных перевозок', N'Поддержка в подготовке таможенных и импортных документов', N'Предоставление сертификатов происхождения и соответствия', N'Поэтапная доставка и возможности складского хранения'),
                    (N'maintenance-after-sales-support', 3, N'Техническое и послепродажное обслуживание', N'Полная техническая поддержка обеспечивает безопасную, эффективную и долговременную работу солнечных систем.', N'Плановое техническое обслуживание и диагностика системы', N'Проверка панелей, инверторов и электрических соединений', N'Мониторинг и анализ показателей выработки энергии', N'Гарантийная и послегарантийная сервисная поддержка'),
                    (N'wholesale-container-supply', 4, N'Toptan Satış ve Konteyner Tedariki', N'Solarix, kurumsal müşteriler ve büyük ölçekli projeler için güneş enerjisi ekipmanlarının toptan satışını gerçekleştirir.', N'Güneş panelleri, inverterler ve enerji depolama sistemleri', N'Tam konteyner — FCL ve yüksek hacimli tedarik', N'Proje gereksinimlerine uygun karma ürün partilerinin hazırlanması', N'Büyük siparişler için özel kurumsal fiyatlar'),
                    (N'longi-official-partner', 4, N'LONGi''nin Azerbaycan''daki Tek Resmî Ortağı', N'Solarix, LONGi''nin Azerbaycan''daki tek resmî ortağı olarak orijinal ve sertifikalı güneş panelleri sunar.', N'Orijinal ve sertifikalı LONGi ürünleri', N'Üretici garantisi ve menşe belgeleri', N'Ürün orijinalliği ve seri numarası doğrulaması', N'Resmî satış, teknik ve garanti desteği'),
                    (N'turnkey-solar-projects', 4, N'Anahtar Teslim Güneş Enerjisi Projeleri', N'Solarix; projelendirme, ekipman tedariki, kurulum ve devreye alma dâhil eksiksiz EPC hizmetleri sunar.', N'Saha incelemesi ve teknik değerlendirme', N'Güneş panelleri ve montaj konstrüksiyonlarının kurulumu', N'İnverter, kablo ve elektrik ekipmanlarının montajı', N'Sistem testleri ve devreye alma'),
                    (N'design-roi-analysis', 4, N'Projelendirme ve ROI Analizi', N'Kurumsal tesisler için optimum sistem gücü, enerji üretimi ve yatırımın geri dönüşü hesaplanır.', N'Elektrik tüketimine göre sistem gücü hesabı', N'Yıllık enerji üretimi ve tasarruf tahmini', N'Geri ödeme süresi ve ROI analizi', N'Ekipman seçimi ve projeye uygunluk değerlendirmesi'),
                    (N'logistics-corporate-supply', 4, N'Lojistik ve Kurumsal Tedarik', N'Yüksek hacimli siparişlerin üreticiden proje adresine kadar teslimat ve tedarik süreci yönetilir.', N'Konteyner ve uluslararası taşımaların koordinasyonu', N'Gümrük ve ithalat belgelerinin hazırlanmasına destek', N'Menşe ve uygunluk sertifikalarının sunulması', N'Aşamalı teslimat ve depolama olanakları'),
                    (N'maintenance-after-sales-support', 4, N'Teknik Servis ve Satış Sonrası Destek', N'Güneş sistemlerinin uzun süre güvenli ve yüksek verimle çalışması için eksiksiz teknik destek sağlanır.', N'Planlı teknik bakım ve sistem teşhisi', N'Panel, inverter ve elektrik bağlantılarının kontrolü', N'İzleme ve enerji üretim verilerinin analizi', N'Garanti ve garanti sonrası servis desteği')
                ) translation ([Slug], [LanguageCode], [Title], [Description], [Content1], [Content2], [Content3], [Content4])
                    ON translation.[Slug] = service.[DetailPageSlug]
                WHERE service.[Category] = 2
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [ServiceManagementLanguages] existing
                      WHERE existing.[ServiceMagamentId] = service.[Id]
                        AND existing.[LanguageCode] = translation.[LanguageCode]);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE service
            FROM [ServiceManagements] service
            WHERE service.[Category] = 2
              AND service.[DetailPageSlug] IN
                  (N'wholesale-container-supply', N'longi-official-partner', N'turnkey-solar-projects', N'design-roi-analysis', N'logistics-corporate-supply', N'maintenance-after-sales-support')
              AND NOT EXISTS (
                  SELECT 1 FROM [ServiceRequests] request
                  WHERE request.[ServiceManagementId] = service.[Id]);

            UPDATE [ServiceManagements]
            SET [IsActive] = 0, [UpdatedAt] = SYSUTCDATETIME()
            WHERE [Category] = 2 AND [DetailPageSlug] IN
                (N'wholesale-container-supply', N'longi-official-partner', N'turnkey-solar-projects', N'design-roi-analysis', N'logistics-corporate-supply', N'maintenance-after-sales-support');
            """);
        migrationBuilder.DropTable(name: "ServiceCategorySettings");
        migrationBuilder.DropIndex(name: "IX_ServiceManagements_DetailPageSlug", table: "ServiceManagements");
        migrationBuilder.DropColumn(name: "DetailContentHtml", table: "ServiceManagementLanguages");
        migrationBuilder.DropColumn(name: "BannerImageUrl", table: "ServiceManagements");
        migrationBuilder.DropColumn(name: "Category", table: "ServiceManagements");
        migrationBuilder.DropColumn(name: "DetailPageSlug", table: "ServiceManagements");
        migrationBuilder.DropColumn(name: "ReadMoreUrl", table: "ServiceManagements");
    }
}
