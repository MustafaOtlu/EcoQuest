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
| F5 / F9 | Envanter ve kaynak kaydını oluştur / yükle |

Vakum: Toplama → Anti-vakum → Su. Su Alev'e, Anti-vakum Duman ve Asit'e etkilidir. Toplama, Salya'yı yavaşça temizler; atıkları ham madde olarak depolar ve su kaynaklarından doldurur. Su modu bitkileri de sular. Kirli su bitkiyi kirletir ve silahı kısa süreli tıkayabilir. Boş depoyu temiz suyla doldurmak kirlenmeyi temizler.

Tohum Silahı: HUD'da Tohum ekimi / Demir top modları görünür. Q bu iki mod arasında geçer. Tohumlar fiziksel olarak fırlatılır; uygun eğimde TerrainCollider veya PlantingSurface üzerine çarpınca ekilir. Duvarlar ve düşmanlar tarla değildir. Ekilen bitkiler arasında boşluk gerekir. Demir toplar Teneke'ye 45, diğer türlere 8 hasar verir. Duvarlarda durur ve atıcıyı yok sayar.

Bitki döngüsü: yeni tohumun suyu azdır. Yeterli suyla yaklaşık 60 saniyede olgunlaşır; büyürken su tüketir. Susuzlukta büyüme durur; 15 saniyelik toleranstan sonra sağlığı azalır. Temiz hasat 3 tohum ve 1 ürün verir. Kirli sulanmış bitki daha yavaş büyür, 1 tohum verir ve yenebilir ürün vermez. Kuruyan bitki geri dönüşümle 1 gübre verir; gübre başka bir bitkiye %20 büyüme ve sağlık desteği sağlar. PlantedSeed.Create, Kenney lahana modelini toprak seviyesindeki kökü sabit kalacak biçimde büyütür. Etkileşim collider'ı filiz küçükken de sulamayı kolaylaştırır; kirlenme ve kuruma modelin rengini değiştirir.

Geri Dönüşüm: Salya'yı temizler, RecyclableResource içeren nesneleri işlem süresi sonunda malzemeye dönüştürür. Vakumla toplanan ham atıklar R ile enerji karşılığında işlenir. Tarayıcı, düşman tür/can/durum, su temizliği, bitki ve atık bilgilerini verir; çevre/rüzgâr simülasyonu henüz yoktur.

Saha istasyonu: MainScene için Eco Field Station prefabı su varili, altı ayrı metal/plastik atık, dört ekim yatağı ve çalışma tezgâhı içerir. Başlangıcın sağ ve arka tarafına yerleştirilir; dış kenarlarda ağaç, kaya, çit ve otlar bulunur. Ekim yatakları ve mevcut Plane ekim yüzeyidir. Eski sahnelerde EcoPracticeArea aynı istasyonu yalnızca sahnede zaten yoksa oluşturur; artık ilkel su silindiri ve üç küp üretmez. Yeni su/atık nesnelerinin ilgili bileşeni ve trigger olmayan collider'ı bulunmalıdır. Model kaynakları, yerleşim ve yeniden oluşturma bilgileri FreeAssets.md içindedir.

Başlangıç kaynakları: 100 silah elektriği, 100 su, 40 demir top, 20 tohum. Silah elektriği kendiliğinden dolmaz; bağlı şarj terminalinde E ile doldurulur. Karakter enerjisi ayrı bir göstergedir ve bitince beş saniyede toparlanır. Düşmanlar etkisiz kalınca fiziksel metal/plastik ganimet bırakır; Toplama ile ham kaynak toplanır, Geri Dönüşüm seçiliyken R ile en fazla sekiz birim işlenir. Her birim iki elektrik tüketir; organik atık gübreye dönüşür. F5/F9 envanteri, YEP'i, organik birikimini ve suyun kirliliğini kaydeder; dünya kaydı henüz tamamlanmamıştır. Ayrıntılar EnergyAndProgression.md içindedir.

Vakumun mevcut boyutu korunur; diğer modeller gerçek mesh sınırlarından boyutlandırılır. Bütün seçili silahlar mevcut Idle/Walk/Run sallanması ve kamera mesafe korumasından yararlanır.

## Kullanıcı kontrol sırası

1. MainScene'de Play'e basıp oyun alanına tıkla. 1–5 ve fare tekerleğiyle araçların ve HUD seçiminin birlikte değiştiğini kontrol et. Sağ/arka tarafta saha istasyonunu bul.
2. Bir atıcının menziline gir: düşmandan fiziksel mermi çıkmalı, hedefe uzanan çizgi görünmemeli. Araya duvar girince saldırı engellenmeli.
3. 2 ile Tohum Silahını seç; HUD'da **Demir top** yazana kadar Q kullan. Teneke'ye ateş et. Can barı azalmalı, yaşayan düşman kısa mesafe savrulmalı. Öldüğünde iki ezilmiş metal kutu oluşmalı.
4. 1 ile Vakumu seç; HUD'da **Toplama** yazana kadar Q kullan. Bir ganimete veya istasyondaki kutu/şişeye sol tık basılı tut. Ham atık sayısı artmalı. 3'e, ardından R'ye basınca metal/plastik artmalı.
5. Diğer düşmanları temizle: Alev için Vakum / **Su**, Duman ve Asit için Vakum / **Anti-vakum**, Salya için **Geri Dönüşüm** kullan. Modları HUD metnine bakarak seç.
6. 2'ye bas; **Tohum ekimi** yazana kadar Q kullan. Bir ekim yatağının toprağına ateş et. Küçük lahana görünmeli. 1'e geçip **Su** modunda sula; nişan alırken nem ve büyüme yüzdesini izle. Yaklaşık bir dakika boyunca yeterli nemle büyümesini sağla.
7. Depo azalınca 1 / **Toplama** ile temiz su variline sol tık basılı tut. Su değeri artmalı. Olgun bitkiye üç metreden yakınken nişan alıp E'ye bas: 3 tohum ve 1 ürün gelmeli. Düşmanlar da bitmişse son bitki yok olduğunda bile görev tamamlandı yazısı kalmalı.
8. F5'e bas, bir tohum veya mermi kullan, F9'a bas. Envanter önceki kayda dönmeli; mevcut bitki/düşmanların geri yüklenmesi beklenmez.

Bu liste güncel sahnenin kullanıcı kabul kontrolüdür; otomatik test sonucu yerine geçmez.
