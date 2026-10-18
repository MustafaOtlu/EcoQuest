# Gökyüzü, hava durumu ve oynanış sunumu

18 Ekim 2026. Çalışma projesi `C:/Users/Musta/Desktop/EcoQuest`, oyun sahnesi `Assets/Scenes/MainScene.unity`. Harita, terrain, bina/ağaç yerleşimi ve kullanıcının çarpıştırıcıları korunur. Önceki klona geliştirme yapılmaz.

## Hava ve görüş

- Oyun başladığında EcoWorldClock, tek bir EcoAtmosphere oluşturur. Resources içindeki `EcoAtmosphere/Sky` URP shader'ı hareketli bulutlar, güneş, gece renkleri ve yalnız gece görünen yıldızlarla varsayılan gökyüzünün yerini alır. Saat, rüzgâr ve bulutlar HUD'da görünür.
- Bulut örtüsü 0,52–0,75 arasında yağmura dönüşür; yağmur kuvveti 0,65'i ve rüzgâr 0,7'yi geçtiğinde durum Fırtına olur. Hava zamanla değişir. Bulut güneş üretimini, rüzgâr türbini ve gün/gece mevcut elektrik sistemini etkiler.
- Açık havada sis 220 birimde başlar, 480'de yoğunlaşır. Dünya kamerasının gerçek uzak kesme düzlemi 500 birimdir; Cinemachine güncellemelerinden sonra da uygulanır. Yağmurda sis yaklaşık 110–400 birime yaklaşır. Sis tek başına nesnelerin Update/fizik hesaplarını durdurmaz; bu aşama kapsamlı CPU optimizasyonu değildir.
- Yağmur kameranın yakınında en fazla 1.800 parçacıkla çalışır. Çatı altında görünür yağmur kesilir; kapalı bitkiler sulanmaz. Açık bitkiler saniyede en fazla 0,025 nem alır. Bölgesel hava kirliliği 55 veya üzerindeyse yağmur sulamak yerine bitkiyi kirletir ve hafif asit hasarı verir. Açık yangınlar yağmurla söner.
- Bileşen kapatıldığında önceki gökyüzü, sis ve kamera uzaklığı geri yüklenir. Su yüzeyi paketine bu çalışma kapsamında müdahale edilmedi.

## Düşmanların oluşma nedenleri

Mevcut EcoRegion alanlarına EnemySceneSetup üzerinden sınırlı doğma bölgeleri bağlanır. Şehrin henüz EcoRegion içermeyen kısımlarına kendiliğinden yeni bölge/yerleşim eklenmez.

| Tür | Oluşma nedeni / hava etkisi | Çevre hedefi |
| --- | --- | --- |
| Duman | Kirli hava | Güneş panelini 8 saniye %50 gölgeler |
| Asit | Hava ve toprak kirliliği; yağmurda ağırlığı artar | Ürüne hasar ve kirlenme |
| Teneke | Kirli toprak | Mevcut oyuncu saldırısı ve sersemletip filtre takma döngüsü |
| Alev | Karbon ve sıcaklık; yağmurda ağırlığı azalır | Sonlu, sınırlı süreli yangın başlatır |
| Salya | Kirli bölgesel doğal su; temiz varil/depo uygun değildir | Doğal su, balık, ürün ve arıtma üzerinde kirletme/yavaşlatma |

Başlangıç gecikmesi 20 saniye, deneme aralığı 20 saniyedir. Bölge başına 3, dünya genelinde 12 canlı düşman; bölge başına günlük 6 yeni düşman sınırı vardır. Başlangıç düşmanları canlı sınırına dahildir. Bileşeni açıp kapamak veya günü geriye almak bütçeyi yenilemez. Temiz bölgeler yeni düşman oluşturmaz.

Doğma noktası oyuncudan en az 12 birim uzakta, bölge içinde, su/yapı/aktörle çakışmayan ve oyuncuya tamamlanmış kara rotası olan bir konum olmalıdır. En fazla 20 konum denenir; uygun yer yoksa doğma gerçekleşmez. Oyuncu yakınsa, görünüyorsa veya düşmanı kışkırtmışsa çevre hedefinden önceliklidir. Saldırı hazırlığında hedef yakalanır; menzil ve görüş yeniden kontrol edilir. İklim canavarı bu çalışmanın kapsamında tamamlanmadı.

## Silah, bitki ve HUD

- Oyuncunun çubuk biçimindeki LineRenderer akışı kaldırıldı. Su hareketli damla ve temas sıçraması, vakum içeri akan toz, anti-vakum dışarı akan parçacıklar, geri dönüşüm yeşil parçacıklar kullanır. Tohum/demir top gerçek mermi gövdeleri ve kısa ayrı tanecik izi gösterir. Kozmetik rastgelelik oyun hesaplarının rastgeleliğini tüketmez; hasar ve kaynak maliyeti korunur.
- Yeni ekilen sebze yaklaşık 28 cm, ağaç yaklaşık 1 m başlangıç görünürlüğüne sahiptir. Gerçek modelin ölçeği ve tabanı ölçülür; büyürken toprağa oturur. Sulama beklerken de görünür; olgun ağacın kalıcı ağaçlandırmaya dönüşmesi korunur.
- HUD koyu zemin, mint vurgu, daha net Türkçe yazılar ve ayrı karakter enerjisi/silah elektriği/su göstergeleri kullanır. Beş slot, seçili araç/mod, gerçek aktif mühimmat, kaynaklar, görev ve hava/saat bilgisi gösterilir. Yazılar 800×600 dahil en az 12 ekran pikselidir. İnşa ve beş saniyelik toparlanma durumları da aynı düzende çalışır.

## MainScene uyumu ve doğrulama

Gerçek sahnede aktif oyuncu `alan/Player (1)` konumundadır; başlangıç düşmanlarının ek konumları bu grubun yerel koordinatlarıyla hesaplanır. Çalışma zamanı navigasyonu aktif oyuncunun yanında oluşturulur. Köken ve taşınmış gruptaki yinelenen dünya servislerinde oyuncuya en yakın etkin saat, aydınlatma/hava/enerji/su için ortak kaynak olur. Ağlar her karede bir kez çalışır; saat devreden çıkarsa kalan saate geçilir.

Unity 6000.0.74f1 kontrolleri izole `.utmp/enemy-build` projesinde çalıştırılır. `SyncSceneValidation.ps1`, MainScene ve referanslarını kopyalayıp sahne hash'ini karşılaştırır; test sahnesi ayrı adla kaydedilir. Kaynak MainScene kaydedilmez/değiştirilmez. Görüntüler `main-scene-images` altına yazılır.

İlgili kontroller: MainSceneReviewCheck, AtmosphereCheck, WorldServicesCheck, EnemyEcologyCheck, EnemySpawnCheck, NavigationCheck, HudVisualCheck, WeaponVisualCheck, SeedVisibilityCheck. Ayrıca mevcut WeaponRuntimeCheck, WaterInfrastructureCheck, PowerCheck ve NatureCheck regresyonları çalıştırılır. HUD 1920×1080, 1280×720 ve 800×600'de taşma ve okunabilirlik açısından sınanır. Bunlar bağımsız Windows dağıtımı ve kapsamlı son performans testinin yerine geçmez.
