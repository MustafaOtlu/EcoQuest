# Bölgesel çevre, fabrika ve yangın

27 Eylül 2026 — yol haritasının 7. aşaması. Son harita yerleşimi değil; mevcut MainScene'deki PowerStarter üzerinden mekanik deneme alanları eklenir. Sahnede kullanıcı nesneleri taşınmaz.

## Oynama

- Başlangıç istasyonu sağlıklı koruluk bölgesindedir. Doğusunda, yaklaşık dünya X=26/Z=8 noktasında fabrika; X=32/Z=2 noktasında kirli su; X=22/Z=-3 noktasında yangın vardır. Bölgeler eşit büyüklüktedir. Son harita aşamasında bu bileşenler gerçek bölgelere yerleştirilecek.
- 4 ile tarayıcıyı seçip zemini tara: bölge, hava/su/toprak kirliliği, karbon endeksi ve sıcaklık görünür. Gökyüzüne bakarken tarama bulunduğun bölgeyi gösterir.
- Marketten 4. YEP seviyesinde büyük filtre al. Fabrikaya üç metre yaklaş, E ile tak. Bir filtre tüketilir, tek sefer 30 YEP kazanılır. Baca beyaza döner, ön yüzde filtre görünür.
- Vakumda Q ile Su moduna geçip yangına ateş et. Temel ekipmanla iki saniye, 20 su ve 10 elektrik tam yangını söndürür. Basınç geliştirmesi söndürmeyi hızlandırır.

## Bağlantılar

- Kirlilik ve karbon 0–100 oyun endeksleridir; gerçek dünya ölçümü değildir. Sıcaklık = bölge tabanı + karbon × 0,08 + hava kirliliği × 0,025.
- Filtresiz fabrika saniyede 0,12 hava kirliliği ve 0,04 karbon ekler. Filtre hava emisyonunu %92 azaltır; karbon yakalama sağlamaz. Takıldığı anda mevcut kirlilik silinmez.
- Bitki örtüsü doğal iyileşmeyi etkiler; yaşayan ağaçlar hava ve karbonu ayrıca yavaşça azaltır. Toplanan her metal/plastik atık birimi bulunduğu bölgenin toprak kirliliğini 0,3 düşürür. Aynı nesne iki kez ödül veya iyileşme vermez.
- Hava kirliliği güneş panelinin üretimini en fazla %60 azaltır; saat, bulut ve fiziksel gölge etkileri de korunur.
- Doğal su kaynağı bölgesindeki su kirliliği 35'in altındaysa temizdir. Pompa ve vakum bu kaliteyi kullanır. İstasyondaki temiz su varili bölgesel kirlenmeye katılmaz. Kaynak temizlenince depodaki eski kirli su kendiliğinden arıtılmaz.
- Toprak kirliliği 40 ve üzerindeyken yetişen ürün kirlenir; mevcut kirli ürünün yarı hızda büyüme/yenilemez hasat kuralları geçerlidir. Sıcaklık 25°C üstüne çıktıkça su ihtiyacı artar.
- İnşa önizlemesi kirli toprak, kirli havada düşük güneş üretimi ve hassas yaşam alanına kurulan türbin/geri dönüşüm tesisi konusunda uyarır. Oyuncunun hassas alana kurduğu bu tesisler bitki örtüsünü azaltır ve toprağı bozar. Sökme/devre dışı kalma bu etkiyi keser. Önceden yerleştirilmiş başlangıç istasyonuna bu ceza uygulanmaz.
- Yangın sonlu yakıt tüketir, hava/toprak/karbonu etkiler, yakındaki yapılara, ürünlere, ağaçlara ve karakter enerjisine zarar verir. Çoklu çarpıştırıcılar hasarı çoğaltmaz. Beş saniyelik toparlanma koruması geçerlidir. Söndürülünce etkiler ve görünmez mermi engeli kaldırılır.

## Kapsam

Fabrika geçici mekanik modelidir; bu aşamada Leonardo/Meshy isteği gönderilmedi. Mevcut ağaç modeli kullanıldı. Yeni ücretli/ücretsiz dış varlık indirilmedi. Duman ve ateş için Unity parçacıkları kullanıldı.

Tam dünya kaydı 11. aşamada; mevcut F5/F9 envanter kaydı bölge/fabrika/yangın durumunu henüz kaydetmez. İklime özgü ürünler, ağaç dikme, balık ve canlı dönüşü 8. aşamadadır. Düşman kaynaklı kirletme ve yangın başlatma 9. aşamada bağlanacak.

Doğrulama: `Tools/UnityChecks/EnvironmentCheck.cs` gerçek MainScene, market, kamera etkileşimi, su silahı, bölgesel üretim ve hasar bağlantılarını sınar. `EnvironmentVisualCheck.cs` filtre öncesi/sonrası ve yangın görüntülerini alır. Kontroller ayrı `.utmp/enemy-build` Unity kopyasında çalışır.
