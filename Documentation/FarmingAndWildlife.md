# Tarım ve ekosistem

27 Eylül 2026 — 8. aşama. Tür, sıcaklık, bakım ve canlı dönüşü GDD'nin döngüsünü uygular; sayısal değerler ilk oynanış dengesi için seçilmiştir.

| Tür | YEP | Sıcaklık | Büyüme | Temiz hasat | 5 tohum |
| --- | --- | --- | --- | --- | --- |
| Lahana | 1 | 8–28 °C | 60 sn | 3 lahana tohumu + 1 ürün | 2 plastik |
| Havuç | 2 | 5–22 °C | 75 sn | 3 havuç tohumu + 2 ürün | 3 plastik |
| Domates | 4 | 18–34 °C | 90 sn | 3 domates tohumu + 3 ürün | 4 plastik |
| Ağaç | 2 | 4–36 °C | 120 sn | Kalıcı ağaç; hasat edilmez | 5 plastik |

Süreler yeterli temiz su, uygun iklim ve böcek olmayan koşullar içindir. Stoklar tür bazında ayrıdır, depo kapasitesi ortaktır. Başlangıç 20 lahana tohumu. Market seçili türden beş tohumu 1 plastik karşılığında alır. Tarla için adet sınırı kaldırıldı; zemin, maliyet ve yerleşim kontrolleri devam eder.

## Kullanım ve bakım

- **2:** Tohum silahı. **Q:** Tohum/demir top. Tohum modunda **R:** Açılan türler arasında geçiş. HUD türü, stoğunu ve uygun sıcaklığı gösterir.
- Mermi ateşlendiği andaki türü taşır; havadayken R'ye basmak atılan tohumu değiştirmez. Hasat gerçek bitkinin türünden tohum verir, o anda seçili türden değil.
- Farklı türlerin su ihtiyacı farklıdır. Yanlış sıcaklıkta büyüme durur ve sağlık azalır. Kirli su/toprak ürünü kirletir; böyle hasat yalnızca bir tohum verir, yenilebilir ürün vermez.
- **1 / Su:** Sulama. **3 / R:** Nişan alınan bitkiye organik gübre. **E:** Hasat. Dönen tohumlar için depoda yer yoksa bitki tüketilmez.
- 45 saniyelik yerleşme döneminden sonra korunmayan sebzelerde böcek baskısı artar; büyüme yavaşlar ve sağlık azalır. Organik gübre böcekleri temizler, 120 saniye korur, uygun iklimde büyümeye destek olur. İklim kuralını atlatmaz.

## Ağaçlandırma ve canlı dönüşü

Ağaçlar ekim yatağına/tarlaya değil açık toprağa dikilir; çevresinde yaklaşık 1,8 metre boşluk gerekir. Sulanan fidan olgunlaşınca tam boy, çarpıştırıcılı ve kalıcı ağaç olur. Ağaçlar hava/su/toprak/karbon değerlerini yavaşça iyileştirir ve bitki örtüsünü destekler. Yanan ağaç canlı desteğini keser ve altı organik birim olarak geri dönüştürülebilir. Yapraklar da organik atık verir; üç işlenmiş organik birim bir gübredir.

Bölgede en az iki sağlıklı ağaç, yeterli bitki örtüsü ve düşük hava/toprak kirliliği varsa yaşam alanı zamanla toparlanır. Önce kuşlar, ardından kelebekler görünür; kirlilik veya ağaç kaybında geri çekilirler. Kuşlar gerçek ürün böcek baskısını azaltır. Bölge başına en fazla üç kuş ve iki kelebek görseli vardır; tarayıcı sayılarını gösterir. Kuş/kelebek modelleri mekanik denemesi için basit hareketli temsillerdir.

## Balıkçılık

- **5 → E:** Kıyıda eller boşken olta at; üç saniye bekle. Tekrar E, uzaklaşmak, silah seçmek, menü veya toparlanma iptal eder.
- Temiz deneme suyu X=-5 / Z=9; kirli su X=32 / Z=2 konumundadır. Son haritadaki göllerin yerine geçmezler.
- Temiz suda nüfus 12'ye, kirli suda 3'e yaklaşır. Yakalama bir balık tüketir; nüfus sıfırken ücretsiz balık verilmez. Su temizlenince nüfus çoğalır.
- Temiz ve kirli balık stokları ayrıdır; ortak kapasite 20. Su sonradan temizlense de yakalanmış kirli balık temizlenmez. Çocuk besleme etkisi 10. aşamada bağlanacak.

## Görseller ve doğrulama

Yeni dış indirme veya Leonardo/Meshy üretimi yapılmadı. Önceden indirilmiş Kenney Food Kit `carrot.fbx`, `tomato.fbx` ve Survival Kit `fish.fbx` kullanıldı. CC0 lisansları korunur. `BuildEcoNatureArt` prefabları ve sahne bağlantılarını tekrar oluşturur.

`NatureCheck` 36 kontrolde gerçek market/mermi/hasat/balıkçılık, iklim, böcek-gübre-kuş ilişkisi, kalıcı ağaç ve envanter kaydını doğruladı. `NatureVisualCheck` ürünleri, canlıları ve dokuz seçenekli marketi render etti. MarketCheck 67, WeaponRuntimeCheck 39, FacilityCheck 32, ResourceProgressionCheck 40, EnvironmentCheck 34 ve ConstructionCheck 26 kontrolle yeniden geçti. Tümü ayrı Unity kopyasında çalıştırıldı.

F5/F9'a tür stokları, seçili tohum ve iki balık stoğu eklendi. Tam dünya kaydı 11. aşamada olduğundan sahnedeki ürün/ağaç/balık nüfusu ve bölge değişiklikleri henüz yeniden açılışta korunmaz.
