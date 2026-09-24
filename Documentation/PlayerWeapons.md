# Silahlar, ekim ve HUD

MainScene'de WeaponHolder üzerindeki PlayerWeaponSystem ve EcoQuestHUD birlikte çalışır. HUD beş slotu, seçili modu, can/enerji/su, mühimmat, tohum ve malzemeleri gösterir. Hedefin üzerinde sulama/hasat/geri dönüşüm ipuçları çıkar. UI bağımsız uGUI bileşenleriyle oluşturulur; kamera sallanmasından etkilenmez.

| Girdi | İşlev |
| --- | --- |
| 1–4 | Vakum / Tohum Silahı / Geri Dönüşüm / Tarayıcı |
| 5 | Boş el |
| Fare tekerleği | Beş slot arasında geçiş |
| Sol fare basılı | Seçili aracı kullan |
| Q | Vakum veya Tohum Silahında mod değiştir |
| E | Yakındaki olgun bitkiyi hasat et; geri dönüşüm silahıyla kurumuş bitkiyi gübreye çevir |
| R | Demir top modunda 1 metalden 5 mermi; geri dönüşümde hedef bitkiye gübre veya ham atıkları işle |
| Esc / oyun alanına tıklama | İmleci bırak / FPS kontrolüne dön |

Vakum: Toplama → Anti-vakum → Su. Su Alev'e, Anti-vakum Duman ve Asit'e etkilidir. Toplama, Salya'yı yavaşça temizler; atıkları ham madde olarak depolar ve su kaynaklarından doldurur. Su modu bitkileri de sular. Kirli su bitkiyi kirletir ve silahı kısa süreli tıkayabilir. Boş depoyu temiz suyla doldurmak kirlenmeyi temizler.

Tohum Silahı: Ekim → Demir Top. Tohumlar fiziksel olarak fırlatılır; uygun eğimde TerrainCollider veya PlantingSurface üzerine çarpınca ekilir. Duvarlar ve düşmanlar tarla değildir. Ekilen bitkiler arasında boşluk gerekir. Demir toplar Teneke'ye 45, diğer türlere 8 hasar verir. Duvarlarda durur ve atıcıyı yok sayar.

Bitki döngüsü: yeni tohumun suyu azdır. Yeterli suyla yaklaşık 60 saniyede olgunlaşır; büyürken su tüketir. Susuzlukta büyüme durur; 15 saniyelik toleranstan sonra sağlığı azalır. Temiz hasat 3 tohum ve 1 ürün verir. Kirli sulanmış bitki daha yavaş büyür, 1 tohum verir ve yenebilir ürün vermez. Kuruyan bitki geri dönüşümle 1 gübre verir; gübre başka bir bitkiye %20 büyüme ve sağlık desteği sağlar. Görseller bu aşamada basit filiz prototipidir.

Geri Dönüşüm: Salya'yı temizler, RecyclableResource içeren nesneleri işlem süresi sonunda malzemeye dönüştürür. Vakumla toplanan ham atıklar R ile enerji karşılığında işlenir. Tarayıcı, düşman tür/can/durum, su temizliği, bitki ve atık bilgilerini verir; çevre/rüzgâr simülasyonu henüz yoktur.

Deneme sahnesi: Enemys üzerindeki EcoPracticeArea, başlangıcın yakınında bir temiz su kaynağı ve üç atık oluşturur. İstenmezse bu bileşen kapatılabilir. Mevcut Plane ekim yüzeyi olarak işaretlidir. Yeni su/atık nesnelerine ilgili bileşen ve trigger olmayan collider eklenebilir.

Başlangıç kaynakları: 100 enerji, 100 su, 40 demir top, 20 tohum. Araç kullanılmazken enerji saniyede 8 yenilenir. Teneke'den 6 metal, Salya'dan 3 plastik kazanılır. Modlar test için açıktır; geliştirme ağacı, mağaza, elektrik şebekesi ve kalıcı kayıt bu aşamada yoktur. Bitkiler ve envanter Play oturumuyla sınırlıdır.

Vakumun mevcut boyutu korunur; diğer modeller gerçek mesh sınırlarından boyutlandırılır. Bütün seçili silahlar mevcut Idle/Walk/Run sallanması ve kamera mesafe korumasından yararlanır.
