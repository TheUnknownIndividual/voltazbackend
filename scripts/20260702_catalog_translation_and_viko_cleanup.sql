SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @CategoryTranslations TABLE (
    ProductCategoryId int NOT NULL,
    LanguageCode int NOT NULL,
    CategoryName nvarchar(255) NOT NULL
);

INSERT INTO @CategoryTranslations (ProductCategoryId, LanguageCode, CategoryName)
VALUES
    (44, 1, N'Günəş panelləri'),
    (44, 2, N'Solar Panels'),
    (44, 3, N'Солнечные панели'),
    (44, 4, N'Güneş Panelleri'),
    (49, 1, N'İnvertorlar'),
    (49, 2, N'Inverters'),
    (49, 3, N'Инверторы'),
    (49, 4, N'İnvertörler'),
    (50, 1, N'Kabel və naqillər'),
    (50, 2, N'Cables and Wires'),
    (50, 3, N'Кабели и провода'),
    (50, 4, N'Kablo ve Teller'),
    (51, 1, N'Elektrik sistemləri'),
    (51, 2, N'Electrical Systems'),
    (51, 3, N'Электрические системы'),
    (51, 4, N'Elektrik Sistemleri'),
    (52, 1, N'Fotovoltaik sistemlərin mühafizəsi'),
    (52, 2, N'PV Protection Systems'),
    (52, 3, N'Системы защиты ФЭС'),
    (52, 4, N'PV Koruma Sistemleri'),
    (53, 1, N'Test'),
    (53, 2, N'Test'),
    (53, 3, N'Тест'),
    (53, 4, N'Test'),
    (54, 1, N'Paylayıcı qutular'),
    (54, 2, N'Distribution Boxes'),
    (54, 3, N'Распределительные коробки'),
    (54, 4, N'Dağıtım Kutuları'),
    (55, 1, N'Yardımçı kontakt blokları'),
    (55, 2, N'Auxiliary Contact Blocks'),
    (55, 3, N'Блоки вспомогательных контактов'),
    (55, 4, N'Yardımcı Kontak Blokları'),
    (56, 1, N'Paylayıcı qutular'),
    (56, 2, N'Distribution Boxes'),
    (56, 3, N'Распределительные коробки'),
    (56, 4, N'Dağıtım Kutuları'),
    (57, 1, N'Metal şkaflar'),
    (57, 2, N'Metal Cabinets'),
    (57, 3, N'Металлические шкафы'),
    (57, 4, N'Metal Panolar'),
    (58, 1, N'Döşəmə tipli şkaflar'),
    (58, 2, N'Floor Cabinets'),
    (58, 3, N'Напольные шкафы'),
    (58, 4, N'Dikili Tip Panolar'),
    (59, 1, N'Sayğac qutuları'),
    (59, 2, N'Meter Enclosures'),
    (59, 3, N'Боксы для счетчиков'),
    (59, 4, N'Sayaç Kutuları'),
    (60, 1, N'Plastik qutular'),
    (60, 2, N'Plastic Enclosures'),
    (60, 3, N'Пластиковые корпуса'),
    (60, 4, N'Plastik Kutular');

MERGE dbo.ProductCategoryLanguages AS target
USING @CategoryTranslations AS source
    ON target.ProductCategoryId = source.ProductCategoryId
    AND target.LanguageCode = source.LanguageCode
WHEN MATCHED THEN
    UPDATE SET CategoryName = source.CategoryName, IsActive = 1
WHEN NOT MATCHED BY TARGET THEN
    INSERT (ProductCategoryId, LanguageCode, CategoryName, IsActive)
    VALUES (source.ProductCategoryId, source.LanguageCode, source.CategoryName, 1);

DECLARE @SubCategoryTranslations TABLE (
    ProductSubCategoryId int NOT NULL,
    LanguageCode int NOT NULL,
    SubCategoryName nvarchar(255) NOT NULL
);

INSERT INTO @SubCategoryTranslations (ProductSubCategoryId, LanguageCode, SubCategoryName)
VALUES
    (31, 1, N'Monokristal panellər'),
    (31, 2, N'Monocrystalline Panels'),
    (31, 3, N'Монокристаллические панели'),
    (31, 4, N'Monokristal Paneller'),
    (32, 1, N'Polikristal panellər'),
    (32, 2, N'Polycrystalline Panels'),
    (32, 3, N'Поликристаллические панели'),
    (32, 4, N'Polikristal Paneller'),
    (33, 1, N'Bifacial panellər'),
    (33, 2, N'Bifacial Panels'),
    (33, 3, N'Двусторонние панели'),
    (33, 4, N'Bifacial Paneller'),
    (34, 1, N'Tam qara panellər'),
    (34, 2, N'Full Black Panels'),
    (34, 3, N'Полностью черные панели'),
    (34, 4, N'Full Black Paneller'),
    (35, 1, N'Yarımkəsimli panellər'),
    (35, 2, N'Half-Cut Panels'),
    (35, 3, N'Панели Half-Cut'),
    (35, 4, N'Half-Cut Paneller'),
    (36, 1, N'Şüşə-şüşə panellər'),
    (36, 2, N'Glass-Glass Panels'),
    (36, 3, N'Стекло-стекло панели'),
    (36, 4, N'Cam-Cam Paneller'),
    (37, 1, N'Elastik panellər'),
    (37, 2, N'Flexible Panels'),
    (37, 3, N'Гибкие панели'),
    (37, 4, N'Esnek Paneller'),
    (38, 1, N'Ağıllı / optimallaşdırılmış panellər'),
    (38, 2, N'Smart / Optimized Panels'),
    (38, 3, N'Умные / оптимизированные панели'),
    (38, 4, N'Akıllı / Optimize Paneller'),
    (39, 1, N'Şəbəkəyə qoşulan invertorlar'),
    (39, 2, N'On-Grid Inverters'),
    (39, 3, N'Сетевые инверторы'),
    (39, 4, N'Şebeke Bağlantılı İnvertörler'),
    (40, 1, N'Avtonom invertorlar'),
    (40, 2, N'Off-Grid Inverters'),
    (40, 3, N'Автономные инверторы'),
    (40, 4, N'Şebekeden Bağımsız İnvertörler'),
    (41, 1, N'Mikro invertorlar'),
    (41, 2, N'Micro Inverters'),
    (41, 3, N'Микроинверторы'),
    (41, 4, N'Mikro İnvertörler'),
    (42, 1, N'Hibrid invertorlar'),
    (42, 2, N'Hybrid Inverters'),
    (42, 3, N'Гибридные инверторы'),
    (42, 4, N'Hibrit İnvertörler');

MERGE dbo.ProductSubCategoryLanguages AS target
USING @SubCategoryTranslations AS source
    ON target.ProductSubCategoryId = source.ProductSubCategoryId
    AND target.LanguageCode = source.LanguageCode
WHEN MATCHED THEN
    UPDATE SET SubCategoryName = source.SubCategoryName, IsActive = 1
WHEN NOT MATCHED BY TARGET THEN
    INSERT (ProductSubCategoryId, LanguageCode, SubCategoryName, IsActive)
    VALUES (source.ProductSubCategoryId, source.LanguageCode, source.SubCategoryName, 1);

UPDATE dbo.ProductTechnologies
SET ProductCategoryId = 55
WHERE Id = 765
  AND Name = N'Auxiliary contact block';

UPDATE dbo.ProductTechnologies
SET ProductCategoryId = 56
WHERE Id = 767
  AND Name = N'Distribution / fuse box';

UPDATE dbo.Products
SET ProductCategoryId = 55,
    ProductSubCategoryId = 54,
    ProductBrandId = 26
WHERE ProductName LIKE N'Viko%'
  AND ProductTechnologyId = 765
  AND ProductCategoryId = 51
  AND ProductSubCategoryId = 45;

UPDATE dbo.Products
SET ProductCategoryId = 56,
    ProductSubCategoryId = 53,
    ProductBrandId = 24
WHERE ProductName LIKE N'Viko%'
  AND ProductTechnologyId = 767
  AND ProductCategoryId = 51
  AND ProductSubCategoryId = 45;

COMMIT TRANSACTION;
