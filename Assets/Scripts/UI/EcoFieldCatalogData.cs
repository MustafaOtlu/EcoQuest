using System;
using System.Collections.Generic;

internal static class EcoFieldCatalogData
{
    public enum Category { Enemies, Tools, Structures, Regions }

    public enum Demo
    {
        None, Vacuum, Seed, Recycler, Scanner, Hands, Flashlight,
        Power, Water, Solar, Wind, Hydro, Farm, Facility, Region, EnemyTin
    }

    public sealed class Entry
    {
        public readonly string Id, Title, Icon, RegionKey;
        public readonly Page[] Pages;

        internal Entry(string id, string title, string icon, string regionKey, params Page[] pages)
        {
            Id = id;
            Title = title;
            Icon = icon;
            RegionKey = regionKey;
            Pages = pages;
        }
    }

    public sealed class Page
    {
        public readonly string Heading, Body, Source;
        public readonly Demo Demo;
        public readonly string[] Icons, Labels;

        internal Page(string heading, string body, Demo demo, string source = "",
            string[] icons = null, string[] labels = null)
        {
            Heading = heading;
            Body = body;
            Demo = demo;
            Source = source;
            Icons = icons ?? Array.Empty<string>();
            Labels = labels ?? Array.Empty<string>();
        }
    }

    private static readonly Entry[] Enemies =
    {
        Enemy("slime", EnemyBrain.Species.Slime, "biodiversity"),
        Enemy("flame", EnemyBrain.Species.Flame, "poi_fire"),
        Enemy("smoke", EnemyBrain.Species.Smoke, "air"),
        Enemy("acid", EnemyBrain.Species.Acid, "w_rain"),
        Enemy("tin", EnemyBrain.Species.Tin, "scrap")
    };

    private static readonly Entry[] Tools =
    {
        Single("vacuum", "Vakum", "tool_vacuum", Demo.Vacuum,
            "Atığı ve suyu çek, dumanı dağıt veya suyla sula. Modu değiştirip örnek alana tıkla."),
        Single("seed", "Tohum aracı", "tool_seed", Demo.Seed,
            "Tohumu uygun toprağa atınca bir filiz çıkar. Demir top moduna geçip Teneke’ye isabet ettirirsen onu kısa süre sersemletirsin."),
        Single("recycler", "Geri dönüşüm aracı", "tool_recycle", Demo.Recycler,
            "Atığı hedefleyip basılı tutarak işle. İkincil tuşla haznedeki ham atığı malzemeye dönüştür."),
        Single("scanner", "Tarayıcı", "tool_scanner", Demo.Scanner,
            "Bir hedefi tara. Tarayıcı suyun kalitesini, filizin nemini ve tesisin çalışma durumunu gösterir."),
        Single("hands", "Eller", "tool_hand", Demo.Hands,
            "Yakındaki nesnelerle etkileş. Örneğin olgun bir bitkinin yanında etkileşim tuşuyla hasat yapabilirsin."),
        Single("flashlight", "Fener", "tool_flashlight", Demo.Flashlight,
            "Sol tıkla feneri aç veya kapat. Açık fener, aracın elektriğiyle önündeki alanı aydınlatır.")
    };

    private static readonly Entry[] Structures = CreateStructures();

    private static readonly Entry[] Regions =
    {
        Region("region_city", "Şehir", "Şehir Merkezi", "region_city",
            "Ev, pazar ve meydanın olduğu güvenli hazırlık alanı. Pazardan malzeme alabilir, evde enerji ve su tasarrufu yapabilirsin.",
            ("poi_home", "Mahalle"), ("poi_market", "Pazar"), ("electricity", "Tasarruf")),
        Region("region_sun", "Güneş Ovası", "Güneşli Çayır", "region_sun",
            "Başlangıç üssünün bulunduğu açık ova. Gölgesiz zeminde güneş paneli kurabilir ve ılık alanda tarım yapabilirsin.",
            ("w_sun", "Açık ova"), ("poi_solar", "Güneş paneli"), ("seed", "Tarım")),
        Region("region_wind", "Rüzgâr Tepeleri", "Rüzgârlı Tepeler", "region_wind",
            "Açık tepelerde rüzgâr türbinleri daha verimli çalışır. Türbin yerini seçerken komşu ormanın hassas habitatını koru.",
            ("w_wind", "Güçlü rüzgâr"), ("poi_tree", "Hassas komşu alan"), ("poi_turbine", "Türbin kur")),
        Region("region_grove", "Orman", "Koruluk", "poi_tree",
            "Zarar görmüş ağaçların olduğu hassas yaşam alanı. Ağaç dikerek bitki örtüsünü güçlendir ve canlıların geri dönmesine yardım et.",
            ("poi_tree", "Ağaç örtüsü"), ("biodiversity", "Zayıf habitat"), ("seed", "Ağaç dik")),
        Region("region_west_lake", "Batı Gölü", "Batı Gölü", "region_lake",
            "Balık Gölü’nün temiz suyu ve balığı sınırlı. Uygun doğal akışta balık geçitli küçük hidroelektrik kurabilirsin.",
            ("water", "Temiz su"), ("fish", "Sınırlı balık"), ("poi_hydro", "Doğal akış")),
        Region("region_east_lake", "Doğu Gölü", "Doğu Gölü", "region_lake_dirty",
            "Fabrika Gölü’ne kirli kanal dökülüyor. Kirletici girişi kapat, suyu pompalayıp arıt ve temiz suyu depola.",
            ("water_dirty", "Kirli su"), ("poi_inlet", "Kirletici giriş"), ("water_quality", "Arıtma hattı")),
        Region("region_industry", "Sanayi Bölgesi", "Sanayi Bölgesi", "region_industry",
            "Filtresiz fabrikalar havayı ve göle akan kanalı kirletiyor. Uygun fabrikalara büyük filtre tak. Teneke ve Duman ile karşılaşabilirsin.",
            ("poi_factory", "Fabrikalar"), ("air", "Kirli hava"), ("filter", "Büyük filtre")),
        Region("region_glacier", "Buzul Dağı", "Buzul Bölgesi", "region_glacier",
            "Dünya karbonu arttıkça buz çizgisi geri çekilir. İzleme istasyonuyla karbonu takip edebilirsin. Soğuk zemin ekime uygun değil.",
            ("w_snow", "Soğuk zemin"), ("carbon", "Karbon baskısı"), ("poi_station", "İzleme istasyonu"))
    };

    public static IReadOnlyList<Entry> GetEntries(Category category) => category switch
    {
        Category.Enemies => Enemies,
        Category.Tools => Tools,
        Category.Structures => Structures,
        Category.Regions => Regions,
        _ => Array.Empty<Entry>()
    };

    private static Entry Enemy(string id, EnemyBrain.Species species, string icon)
    {
        var lesson = EcoEnemyTutorial.LessonFor(species);
        var real = EnemyPage("Gerçek hayat", lesson.Explanation, lesson.Source, lesson.Real, Demo.None);
        var demo = species == EnemyBrain.Species.Tin ? Demo.EnemyTin : Demo.None;
        var game = EnemyPage("Oyunda", lesson.Threat + " " + lesson.Technique, "", lesson.Game, demo);
        return new Entry(id, EnemyBrain.NameOf(species), icon, "", real, game);
    }

    private static Page EnemyPage(string heading, string body, string source, EcoEnemyTutorial.Cue[] cues, Demo demo)
    {
        var icons = new string[cues.Length];
        var labels = new string[cues.Length];
        for (int i = 0; i < cues.Length; i++)
        {
            icons[i] = cues[i].Icon;
            labels[i] = cues[i].Label;
        }
        return new Page(heading, body, demo, source, icons, labels);
    }

    private static Entry Single(string id, string title, string icon, Demo demo, string body)
        => new Entry(id, title, icon, "", new Page("", body, demo));

    private static Entry Region(string id, string title, string key, string icon, string body,
        (string Icon, string Label) condition, (string Icon, string Label) issue, (string Icon, string Label) action)
        => new Entry(id, title, icon, key, new Page("", body, Demo.Region,
            icons: new[] { condition.Icon, issue.Icon, action.Icon },
            labels: new[] { condition.Label, issue.Label, action.Label }));

    private static Entry[] CreateStructures()
    {
        var buildings = EcoBuildingCatalog.Entries;
        var entries = new Entry[buildings.Length + 2];
        for (int i = 0; i < buildings.Length; i++)
        {
            var building = buildings[i];
            var (icon, demo, connection) = building.Id switch
            {
                "solar" => ("poi_solar", Demo.Solar, "Üretimi ağa aktarmak için elektrik kablosu kullan."),
                "wind" => ("poi_turbine", Demo.Wind, "Açık alana kur ve elektrik kablosuyla ağa bağla."),
                "battery" => ("electricity", Demo.Power, "Üretici ve tüketiciyi elektrik kablolarıyla bataryaya bağla."),
                "charger" => ("poi_charge", Demo.Power, "Elektrik kablosunu bağladıktan sonra istasyonla etkileş."),
                "intake" => ("water_dirty", Demo.Water, "Su kaynağına yakın kur. Elektrik ver ve çıkışını uygun boruya bağla."),
                "purifier" => ("water_quality", Demo.Water, "Elektrik ver. Girişe kirli, çıkışa temiz su borusu bağla."),
                "tank" => ("water", Demo.Water, "Temiz su borusuyla suyu depoya getir ve çıkışı tarlaya bağla."),
                "recycling" => ("tool_recycle", Demo.Facility, "Tesisle etkileşerek ham atığı bırak ve işlenenleri al."),
                "farm" => ("seed", Demo.Farm, "Sulama için temiz su borusu bağla."),
                "hydro" => ("poi_hydro", Demo.Hydro, "Balık geçitli doğal akışta kur. Elektriği kabloyla ağa aktar."),
                _ => ("ui_build", Demo.None, "")
            };
            string body = EcoBuildingCatalog.Purpose(building.Id) + " " + connection
                + "\n" + building.Metal + " metal · " + building.Plastic + " plastik";
            entries[i] = Single(building.Id, building.Name, icon, demo, body);
        }
        entries[buildings.Length] = Single("cable", "Elektrik kablosu", "electricity", Demo.Power,
            "Üretici, batarya ve tüketiciyi aynı elektrik ağına bağlar. Sırayla iki bağlantı ucunu seç.\nMetre başına 1 metal · 1 plastik");
        entries[buildings.Length + 1] = Single("pipe", "Su borusu", "water", Demo.Water,
            "Temiz veya kirli su hattını seç. Önce çıkışa, sonra girişe tıkla. Boru suyu seçtiğin yönde taşır.\nMetre başına 2 plastik");
        return entries;
    }
}
