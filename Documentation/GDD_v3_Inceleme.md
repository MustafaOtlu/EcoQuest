# Akış İncelemesi: EcoQuest GDD v3

> **Kapsam ve yöntem:** `GDD.md` (v3, 20 Ekim; 27 Ekim'e kadar eklenen kararlar ve 100 satırlık "Uygulama durumu" günlüğüyle) baştan sona okundu. Bir konuda birden çok kayıt varsa en son tarihli kayıt güncel sayıldı (ör. 27 Ekim bölge adları, hedef yönlendirmesi). Birkaç sayı kodla karşılaştırıldı (`EcoWorldClock`, `EcoPowerNode`, `EnemyBrain`, `EcoCropCatalog`). Bu bir **masa başı incelemedir**: tempo, merak ve keyif ancak gerçek oyuncu testinde bilinir. Puanlar ve hedef değerler başlangıç hipotezidir.


> **Düzeltme (27 Ekim, uygulama sırasında):** Bu raporun "ilk gece P1'i kilitliyor" bulgusu yanlıştı. Kod okunurken görüldü ki üste hazır bir güneş paneli, **rüzgâr türbini**, 80 EB'lik batarya ve şarj istasyonu var ve P1 bu ağı sayıyor. Yani P1 gece de tamamlanabiliyordu. Gece ve gündüz oranı ile ilk gün saati yine de kullanıcı kararıyla değiştirildi (S1). Önerilen düzeltmeler de uygulandı, ama farklı gerekçeyle. İlk oturumdaki gerçek sorunlar şunlardı:
> - Yön oku P1 için Güneş Ovası'nın ortasını gösteriyordu.
> - Proje adımı ancak hasattan sonra çıkıyordu.
> - Kart somut işi ve tuşu söylemiyordu.
>
> Ayrıntı ve güncel durum: `GDD_v4.md` B bölümü ve §21.

## Kısa hüküm

Çevre konusunu seven ve sakin oyunları seven oyuncu (hedef kitle: TÜBİTAK projesi kapsamında öğrenciler ve genç yetişkinler) **ilk projeyi bitirirse büyük olasılıkla 1–2 saat kalır**. Oyunun kalbi olan "bir sistem kurdum, çalışıyor, çevre değişiyor" hissi iyi tasarlanmış: neden-sonuç açık, hata cezası hafif, ödüller tek seferlik ve bilgi veriyor.

En büyük bırakma riski **ilk 20 dakikada**, özellikle **6.–13. dakikalar** arasında:
- Oyun 09:00'da başlıyor ve bir gün 15 dakika sürüyor. Güneş 18:00'de batıyor, yani **oyunun 5,6. dakikasında gece oluyor** ve güneş paneli 7,5 dakika boyunca hiç üretmiyor.
- Oyuncu ilk enerji projesini (P1) tam bu gecede kurmaya çalışıyor. Panel üretmiyor ve oyun bunun nedenini söylemiyor.

İkinci risk **20.–60. dakikalar arasında bir düzlük**. P1 bittikten sonra beş proje aynı anda açılıyor. Göl projesi (P2) yaklaşık 70 plastik istiyor, eldeki ise 16. Oyuncu ne yapacağını biliyor ama neden yaptığını hissetmeyebilir.

Tek bir bırakma noktası yok; ama doruklar zayıf. 3–5 saatlik oyunda tek planlanmış doruk final. Projelerin bitiş anı şu an yalnız bir bildirim.

| # | Sorun | Katman | Etkisi |
|---|---|---|---|
| 1 | İlk oturum planı (§4) oyundaki gerçek akışla örtüşmüyor; ilk gece 5,6. dakikada geliyor ve P1'i kilitliyor | K0 / K3 | İlk anlamlı başarı 13–20. dakikaya kayıyor; ilk başarısızlığın nedeni görünmüyor |
| 2 | Yönlendirme kuralı kendisiyle çelişiyor: §6 "sürekli görev oku yok" diyor, 27 Ekim'de sürekli görev oku eklendi | K0 / K4 | Ekip neyi korumak istediğini bilmiyor; ok kalıcı olursa keşif merakı söner |
| 3 | Tempo eğrisi tanımlı değil: proje bitişleri doruk değil, gecenin oranı (günün %50'si) bilinçli seçilmemiş | K2 | Uzun ortada düzlük; "sakin" ile "olaysız" arasındaki çizgi belirsiz |
| 4 | P1'den P2'ye geçişte plastik darboğazı ve görünmeyen maliyet | K2 / K3 | 20–40. dakikalarda amaçsız hasat/satış döngüsü |
| 5 | Belge sağlığı: güncel tasarım, eski analiz (§18) ve 100 satırlık günlük aynı dosyada; birçok bölüm eski ad ve eski kararla kalmış | Belge | Yanlış kural uygulanma riski (ör. §16 anketi "uygulanmadı" diyor) |

## Deneyim hedefi

**Belgenin kendi hedefi (§1):** "Bir sistemi kurdum, çalışıyor ve çevre değişiyor" hissi. Sakin tempo, tok etkileşim ve yaşayan çevre. Bu hedef net ve güçlü, ama belgede bu hedefe bağlı **deneyim sütunları yok**. Bu yüzden yeni özelliklerin hangi hedefe hizmet ettiği sorulmamış. Örnek: 27 Ekim'de eklenen hedef oku keşif hissine zarar verebilir, ama bunu tartacak bir ölçüt yok.

**MDA estetikleri (tahmini öncelik):**
1. **İfade ve keşif:** Yerleşimi kendi düzeninle kurmak, nedenleri bulmak.
2. **Anlatı:** Çocuk ve değişen dünya.
3. **Zorluk:** Hafif; sistem kurma ve final.
4. **Teslimiyet:** Sakin vakit geçirme.

**Lazzaro'nun dört anahtarı:**

| Anahtar | Durum | Nerede |
|---|---|---|
| Easy Fun | Var | Nedenleri keşfetme, canlıların geri dönmesi |
| Altered States | Var | Sakin tempo, müzik, akşam ışıkları |
| Hard Fun | Zayıf ama var | Türe uygun müdahale, final |
| People Fun | Yok | Tek oyunculu; sınıf ortamında paylaşma potansiyeli var |

Üç anahtar kapsanıyor.

**İmza an adayları (M12):**
- **A. "Göl geri geliyor":** Fabrika Gölü'nün rengi döner, balıklar geri gelir, kart oksijeni 3,8 → 7 mg/L gösterir. Görsel, anlamlı ve oyuncunun kendi kurduğu sisteme bağlı. **En güçlü aday.** Şu an bu an bir bildirime indirgenmiş.
- **B. Final:** Üç odağı dengelemek. Fiero üretir ama oyuncuların bir kısmı finale hiç ulaşmayacak.
- **C. İlk gece:** Batarya sayesinde ışıkların yanması. Doğru kurulursa "depolama işe yarıyor" dersini duyguyla verir. Şu an bu an bir **hayal kırıklığı**: panel duruyor ve açıklama yok.

**Hedef ile zamanın örtüşmesi:** Oyuncunun en çok zaman geçirdiği yer muhtemelen ekim/sulama/atık toplama ve inşa. Kalp etkinliği ise sistem kurup sonucunu görmek. Bu ikisi örtüşüyor, ancak **sonuç anı cilalanmamış** (bkz. Sorun 3).

## Katman puanları

| Katman | Puan (0–5) | Gerekçe |
|---|---|---|
| K0 Erişim | 3 | Kontroller platform alışkanlıklarına uygun ve yeniden atanabiliyor; erişilebilirlik bölümü güçlü (§13). Ancak §4'teki ilk 20 dakika planı oyundaki sıralamayla çelişiyor; 13 tuş grubu ilk oturumda birden açılıyor; yönlendirme kuralı kendisiyle çelişiyor (§6 ve 27 Ekim kaydı) |
| K1 Anlık döngü | 4 | §5'te her eylemin görsel ve ses karşılığı tanımlı, yanlış işlem açıklanıyor, sonsuz sersemletme engellenmiş. Eksik: hangi geri bildirimin başarıya bağlı olduğu ayrıca belirtilmemiş; altı araç ve üç mod ilk saatte yük |
| K2 Zorluk ve tempo | 2 | Düşman yoğunluğu ve final ritmi sayılarla tanımlı (§11, §12). Oyunun geneli için faz ve duygu eğrisi yok; gece ve gündüz oranı tanımlanmamış (kodda gecenin oranı %50); acemi ile deneyimli oyuncu için tek ayar var |
| K3 Hedef ve ilerleme | 4 | Hedef hiyerarşisi açık (§4: saniye, 2–5 dk, 20–35 dk, 3–5 sa); projeler tek seferlik ve ölçülebilir; 120 saniyelik ilerleme görünür (§7). Eksik: uzmanlıkta 9 hakkın 9 kademeye eşit olması, sonda herkesin her şeyi aldığı anlamına geliyor; seçim yalnız sıralamada kalıyor |
| K4 Merak ve keşif | 3 | Nedenler tarama ile açılıyor, tarifler keşifle açılıyor (§7). Ama coğrafya baştan görünür, bütün bölgeler açık, sürpriz kaynağı yalnız hava durumu. Yeni gerçek ölçüler (AQI, mg/L) merak için iyi bir kanca, ama belgede bu amaçla kullanılmamış |
| K5 Anlam ve bağ | 4 | İçsel fantezi güçlü: oyuncunun becerisi (doğru aracı ve doğru altyapıyı seçmek) öğretilmek istenen kavramın kendisi (Malone). Çocuk empati kuruyor, projeleri seçmek özerklik veriyor. Eksik: People Fun yok; çocuğun tepkileri az sayıda |

## Korunması gereken güçlü yanlar

- **Ceza hafif ama anlamlı (§11):** Beş saniyelik toparlanma ve mevcut elektriğin %20'si. Oyuncu korkmadan dener, ama dikkatsizliğin küçük bir bedeli var. Bu, sakin tempoyu bozmadan risk hissi veriyor. Ceza büyütülmemeli.
- **Ödüller tek seferlik ve bilgi veriyor (§7):** YEP sömürülemiyor, ganimet YEP vermiyor. Ödül davranışı kontrol etmek için değil, bilgi vermek için kullanılıyor (SDT). Grind teşviki yok.
- **Türe uygun müdahale (§11):** Her tehdit farklı bir fiil istiyor (su, anti-vakum, demir + filtre, vakum). Rol matrisi temiz; iki düşman aynı cevabı istemiyor (Koster).
- **Neden ve sonuç zinciri (§10):** Filtre havayı düzeltiyor ama karbonu düzeltmiyor. Bu, oyuncunun yanlış olan sezgisini oyunun içinde düzeltir; eğitsel hedefi mekaniğe gömer.
- **Çıkmaza girmeme (§8):** Acil şarj, temel tohum, atölye. Oyuncunun elinde olmayan nedenlerle takılmasını önlüyor (GameFlow).
- **Erişilebilirlik ve kabuk kontrolü (§13, §14):** Tuş atama, renk körü paleti, onaylı yükleme, üç slot. K0 için çok iyi bir temel.

## Oyuncu yolculuğu: nerede bırakırlar?

Hesap: Gün 900 saniye, yani bir oyun saati 37,5 gerçek saniye. Başlangıç 09:00, gün batımı 18:00 (`SolarFactor = sin((saat−6)·π/12)`), lahana 180 saniyede büyüyor.

| Zaman | Oyuncunun yaşadığı | Bırakma riski | Mekanizma (katman) |
|---|---|---|---|
| 0–10 sn | Menü, Yeni oyun, üs; hedef kartı "Mahalle çocuğuyla konuş" ve 81 m'lik ok | Düşük | Net ilk hedef (K0) |
| 10 sn–2 dk | Şehre yürüyüş, çocukla konuşma | Düşük | Amaç veriliyor; yürüyüş kısa (K3) |
| 2–5 dk | Tohum ek ([2]), sula ([1], [Q]), büyürken atık topla | Düşük–orta | Üç yeni araç ve mod birden (K0) |
| **5,6 dk** | **Gün batıyor; panel bundan sonra 0 EB üretiyor** | — | — |
| 6–13 dk | Hasat; ardından hedef "Proje: Mahalleye ilk enerji". Oyuncu paneli, bataryayı ve şarjı kuruyor ama hiç şarj olmuyor | **Yüksek** | Nedeni görünmeyen engel; "neden yapamıyorum?" akış bozucusu (GameFlow) (K0/K2) |
| 13–20 dk | Sabah oluyor; 10 EB şarj, 120 saniyelik doğrulama, +40 YEP, öğrenme kartı | Orta | İlk büyük başarı geç geliyor (K3) |
| 20–60 dk | Beş proje açık; göl için yaklaşık 48 metal ve 70 plastik gerekiyor, ama maliyet önceden görünmüyor | Orta | Görünmeyen maliyet ve düzlük (K2/K3) |
| 1–3 sa | Projeler, siparişler, tehditler; her proje bitişi bir bildirim | Orta | Doruk yok, tekrar yorgunluğu (K2) |
| 3–5 sa | %50 hazırlıkla final, zafer, serbest oyun | Düşük | Net doruk (K2/K5) |

## Kritik sorunlar (öncelik sırasıyla)

### 1. İlk oturum planı ile gerçek akış farklı; ilk gece P1'i kilitliyor (K0 / K3)

- **GDD'de ne var:** §4'teki "İlk 20 dakika" tablosu şu sırayı yazıyor: çocuk → atık tarama → 7–12. dakikada panel/batarya/şarj → 12–16. dakikada şarj → ilk proje sonucu. Oyunda hedef zinciri ise farklı (27 Ekim kaydı): çocuk → tohum → sulama → hasat → P1. §10 "başlangıç sabah 08.00" diyor; kod 09:00'da başlıyor. Gece uzunluğu belgede hiç tanımlanmamış. Kodda güneş 06:00–18:00 arasında, yani gecenin oranı %50.
- **Hesap:** 09:00'dan 18:00'e 9 saat × 37,5 sn = **5,6 dakika**. Gece 12 saat × 37,5 sn = **7,5 dakika** sürüyor. P1'in sağlam koşulu ("ağ doğrulama boyunca üretim veya rezervle şarj istasyonunu besleyebilir") ilk gecede yalnız boş bir bataryaya dayanıyor.
- **Neden sorun:** İlk başarısızlık oyuncunun elinde olmayan bir nedene bağlı ve açıklanmıyor. Bu, kontrol kaybı hissi yaratan bir akış bozucusu (GameFlow). İlk dakikalar herkes için oynanabilir olmalı (Chen 2007). Oysa bu an doğru kurulursa oyunun en iyi öğretme anına dönüşebilir: "güneş yok ama batarya var".
- **Öneri:**
  - **[Açık soru]** Gece uzunluğunu tasarım kararı olarak yazın. Öneri: gün ışığı 06:00–20:00 ve oyun günün üçte ikisi gündüz geçsin. İlk gün 07:00'de başlasın. Böylece ilk gün batımı yaklaşık 9. dakikada gelir, gece yaklaşık 5 dakika sürer.
  - **[Karar içinde]** Üsteki başlangıç bataryası yarı dolu başlasın (120/240 EB). İlk gün batımında hedef kartında tek satırlık bir uyarı çıksın: "Güneş batıyor · panel durur, batarya gece işi sürdürür."
  - **[Karar içinde]** Hedef zincirinde P1 kurulumu, lahana büyürken araya girsin (çocuk → ek ve sula → büyürken panel/batarya/şarj kur → hasat). Böylece panel, güneş batmadan önce üretip bataryayı doldurur.
  - **Amaç:** İlk büyük başarıyı 12. dakikanın altına çekmek ve geceyi bir öğretme anına çevirmek.
  - **Oyuncunun deneyimi:** "Gece oldu ama ışıklarım yanıyor, çünkü batarya kurdum."
  - **Diğer sistemlerle ilişkisi:** P5 dayanıklılık projesinin dersini erken tohumlar; §16'daki "güneşsiz zamanda üretim ve depo" öğrenme hedefini doğrudan besler.
  - **Yan etki:** Gece kısalınca sokak lambası ve akşam atmosferi daha az görülür. Kısalma %50'den %33'e indiği için kabul edilebilir.
- **Ölçüm:** İlk P1 tamamlanma süresi (hedef ≤ 15 dk). İlk gecede şarj istasyonu yanında geçen ve boşta kalınan süre. Oturum sonunda "Gece neden şarj olmadı / oldu?" sorusu.

### 2. Yönlendirme kuralı çelişiyor (K0 / K4)

- **GDD'de ne var:** §6 "Normalde sürekli görev oku yoktur. İsteğe bağlı üç ipucu kademesi: bölgeyi hatırlat → sorunun türünü açıkla → ilgili noktayı geçici işaretle" diyor. 27 Ekim kaydı ise her zaman açık bir hedef işareti ve ekran kenarı oku ekledi (kullanıcı isteği: "Oyuncu için yönlendirmeler eksik").
- **Neden sorun:** En son kullanıcı kararı geçerli ve doğru bir ihtiyaca cevap veriyor; yönsüz kalan oyuncu ilk dakikalarda bırakır (K0). Ama ok her zaman açık kalırsa, oyuncu nedeni keşfetmek yerine oku izler. Merak, devam etmeyi keyiften daha iyi öngörüyor (Kao 2024); gizli bilginin kademeli açılması merakın kaynağı (Malone 1980). Belgede iki kuralın hangi aşamada geçerli olduğu yazılı değil.
- **Öneri:**
  - **[Karar içinde]** İki kuralı aşamaya bağlayın. Ok ve hedef işareti **öğretici zincirde** (çocuk, ekim, hasat, P1) her zaman açık olsun. P1 bittikten sonra projelerde ok, §6'daki ipucu kademelerinin üçüncüsü olsun: J günlüğünde "Yerini göster"e basınca 60 saniye görünsün. Ayarlarda "Hedef oku: Her zaman / Öğretici / Kapalı" seçeneği olsun.
  - **Amaç:** Erişimi korurken orta oyunda keşfi oyuncuya bırakmak.
  - **Oyuncunun kararı:** Yardım isteyip istememek oyuncunun elinde kalır (özerklik).
  - **Yan etki:** Ok kapandıktan sonra kaybolan oyuncu olabilir; bunu ölçün.
- **Ölçüm:** P1 sonrası "Yerini göster" kullanım oranı. P2'ye başlama süresi. Oturum sonunda: "Ne yapacağımı bilmediğim anlar oldu" (1–5).

### 3. Tempo eğrisi yok; proje bitişleri doruk değil (K2)

- **GDD'de ne var:** §4'te süre ölçekleri tablosu var; §12'de finalin ritmi saniyesine kadar tanımlı. Ancak 3–5 saatlik oyunun genelinde gerilim ve rahatlama, dinlenme noktası ya da ara doruk tanımlanmamış. Proje tamamlandığında (§7) yalnız "+40 YEP" ve öğrenme kartı var.
- **Neden sorun:** Sabit tempo, dalgalanan tempodan daha düşük akış üretiyor (Baumann 2016). "Sakin" bir oyunda da doruk gerekir; doruk burada savaş değil, **dönüşüm anı** olmalı. Başarıya bağlı ve olayın önemiyle orantılı geri bildirim yeterlilik hissini artırır (Kao 2024; Hicks 2019).
- **Öneri:**
  - **[Karar içinde]** Her ana proje bitişinde 4–6 saniyelik bir "dönüşüm anı" olsun. Kamera hafifçe bölgeye döner. Renk geçişi, canlıların gelişi, müzikte kısa bir motif. Bölge kartındaki gerçek ölçü sayılarla değişir ("AQI 169 → 62 · sağlıksız → orta"). Ardından çocuğun tek cümlelik tepkisi gelir.
  - **[Açık soru]** Faz başına duygu eğrisini GDD'ye yazın: açılış (merak), ilk gece (hafif kaygı ve rahatlama), orta oyun (proje seç, kur, doruk; ardından rahat bakım), final (gerilim ve fiero), serbest oyun (huzur).
  - **Diğer sistemlerle ilişkisi:** Bu anlar yeni gerçek ölçüleri (27 Ekim) anlamlı kılar. Altı proje altı doruk verir.
  - **Yan etki:** Kontrolü birkaç saniye alır; atlanabilir olmalı (M8).
- **Ölçüm:** Oturum sonunda "Bu oyundan arkadaşına hangi anı anlatırdın?" sorusu. Proje bitişinden sonraki 2 dakikada oyunu bırakanların oranı.

### 4. P1'den P2'ye geçişte görünmeyen maliyet ve plastik darboğazı (K2 / K3)

- **GDD'de ne var:** Başlangıç 50 metal ve 40 plastik; P1 zinciri 34 metal ve 24 plastik (§8). Elde 16 metal ve 16 plastik kalıyor. P2 için pompa (8/8), arıtma (20/16), depo (10/14), yaklaşık 15 m boru (0/30) ve yaklaşık 10 m kablo (10/10) gerekiyor. Toplam yaklaşık **48 metal ve 78 plastik**.
- **Hesap:** Plastik açığı yaklaşık 62. Lahana bir plastik getiriyor; dört yuva 180 saniyede 8 ürün = 8 plastik, yani tarımla yaklaşık **23 dakika**. Salya ganimeti 5 plastik (`EnemyBrain`), ve Fabrika Gölü'nde Salya var. Bu iyi bir teşvik uyumu, ama belgede yazılı değil ve oyuncuya gösterilmiyor.
- **Neden sorun:** Hedefe ne kadar yakın olduğunu bilmeyen oyuncu yön kaybeder (GameFlow). Emek ile ödül orantılı olmalı (GameFlow). Bu hesap rasyonel oyuncu varsayımıdır ve gerçek davranışı playtest gösterir.
- **Öneri:**
  - **[Karar içinde]** J günlüğündeki proje kartı "tahmini malzeme: ~48 metal · ~78 plastik (sende: 16 · 16)" satırını göstersin.
  - **[Karar içinde]** Göl projesi için, kıyıda pompa–arıtma–depoyu 6 m içinde kurmaya uygun düz bir alan işaretlensin. Böylece boru maliyeti 30'dan yaklaşık 12 plastiğe iner.
  - **[Karar içinde]** Hedef kartı malzeme eksikken "Plastik topla · Salya (göl kıyısı) 5 plastik, temiz hasat 1–2" desin.
- **Ölçüm:** P1 bitişinden P2 kurulumuna kadar geçen süre (hedef ≤ 25 dk). Bu aralıkta boşta kalınan süre.

### 5. Belge sağlığı (belge yapısı)

- **GDD'de ne var:**
  - §6 tablosunda "Koruluk sonrası", §7'de "Doğu gölü", "Koruluk", "Sanayi" gibi eski adlar kalmış.
  - §16 anketi "şu an uygulanmayan" olarak anlatıyor; anket uygulandı ve sorular 27 Ekim'de değişti.
  - §17 "test bu GDD tamamlanana kadar ertelendi" diyor; günlükte onlarca gerçek girdi testi var.
  - §18 bir 20 Ekim anlık görüntüsü (Size=200, MainScene hedefi, "hidro yok"). Çoğu artık yanlış.
  - Belge başlığı "Bu revizyonda test yapılmadı" diyor.
  - §3 beş aracı anlatıyor; fener altıncı araç olarak eklendi.
  - §13 gövde metnini 1080p'de 20–22 px olarak tanımlıyor; oyunda birçok etiket 15–17 px.
- **Neden sorun:** Güncel kural ancak günlük okunarak bulunuyor; eski karar güncel sanılırsa yanlış şey uygulanır (uygulama sezgisi).
- **Öneri:** **[Karar içinde]** Yeni sürüm şu yapıda olsun:
  - Güncel tasarım tek yerde.
  - Karar kaydı tarih, gerekçe ve yerine geçtiği kuralla ayrı tutulsun.
  - Açık sorular ayrı listede olsun.
  - Sayılar tek tabloda toplansın.
  - Ayrıntılı günlük eski belgede arşiv olarak kalsın.

## Sistem analizleri

**Tutarlılık taraması (M2):**

| Konu | A | B | Oyuncuya etkisi | Güncel olan |
|---|---|---|---|---|
| Görev oku | §6: sürekli ok yok | 27 Ekim: her zaman açık ok | Keşif ile erişim çatışması | 27 Ekim; aşamaya bağla (Sorun 2) |
| Başlangıç saati | §10: 08.00 | Kod: 09:00 | İlk gece bir saat (37,5 sn) erken geliyor | Karar ver (Sorun 1) |
| İlk 20 dakika | §4: atık → panel → şarj | Hedef zinciri: tohum → sulama → hasat → P1 | Plan oyunu anlatmıyor | Zinciri yeniden yaz |
| Anket | §16: uygulanmıyor, eski 5 konu | 27 Ekim: v2 soruları uygulandı | — | 27 Ekim |
| Araç sayısı | §3: 1–5 | 23 Ekim: 6. araç fener | Tuş tablosu eksik | Fener ekle |
| Metin boyu | §13: 20–22 px | Uygulama: 15–17 px etiketler | Okunurluk | Hedefi gözden geçir (açık soru) |
| Ana sahne | §2 ve §18: MainScene | Günlük: oyun EcoWorld'den çalışır | — | EcoWorld |
| Bölge adları | §6, §7, §16: eski adlar | 27 Ekim: yeni adlar | Proje metinleri karışık | Yeni adlar |

**Sayı sağlaması (M3):**
- **YEP:** 6×40 + 3×20 + 3×20 = 360 → 9 hak. Bu da 3 dal × 3 kademe = 9 kademe eder. Tüm ödülleri alan oyuncu **bütün kademeleri** alır; uzmanlık seçimi yalnız sıralama kararıdır. Bu bir sorun mu? Hedef "sakin, herkes bitirsin" ise kabul edilebilir. Ama "uzmanlaşma" adı vaat ettiğini vermiyor. Bkz. ikincil bulgular.
- **Tarım ekonomisi:**
  - Domates 4 yuvada 5 dakikada 16 ürün = 48 metal ve 32 plastik. Lahana aynı sürede yaklaşık 27 metal ve 13 plastik getiriyor.
  - Domates baskın strateji (Güneş Ovası 20 °C ve uygun). Bu bilinçli bir "daha zor ürün, daha çok kazanç" ise sorun değil. Gecikmeli açılması (ipucu sonrası) bu yüzden doğru.
- **Ganimet ve kirlilik teşviki (M4):**
  - Salya 5, Asit 3, Duman ve Alev 2 plastik; Teneke 6 metal (eksi 2 metal ve 1 plastik filtre).
  - Bölge başına günde en çok 6 doğuş var. Kirli 4 bölgede bu en çok yaklaşık 24 tehdit ve günde yaklaşık 70 plastik eder.
  - Domatesle günde (15 dk) yaklaşık 96 plastik kazanılıyor.
  - Sonuç: kirliliği sürdürmek tarımdan daha kârlı değil; §8'deki "bu olasılık gizlenmez" kararı güvenli.

**Kontrol kaybı bütçesi (M8):** Toparlanma 5 saniye, sonra 3 saniye dokunulmazlık; Teneke sersemlemesi oyuncuya değil düşmana. Düşmanlar yakın vuruş veya mermi kullanıyor ve birden çok sersemletme kaynağı yok. Bütçe sağlıklı.

## İkincil bulgular

- **Uzmanlık seçimi (K3/K5):** **[Karar içinde]** Her dalın 3. kademesi iki seçenekten biri olsun. Örnek, Enerji 3: "gece bataryası +%25" veya "rüzgâr eşiği düşük". Ücretsiz yeniden dağıtım korunur. Böylece seçim kimlik kazanır.
- **İlk oturumda araç yükü (K0):** Fener, gece gelmeden açılmamalı. İlk gün batımında "[6] Fener" ipucuyla tanıtılsın (M6: döngü başına bir yeni kural).
- **Anket tavan etkisi (§16):** v2 soruları "herkesin anlayacağı" düzeyde. Ön testte çok yüksek doğru oranı çıkarsa öğrenme farkı ölçülemez. Ön testte doğru oranı %80'i geçerse 2 soruyu daha ayırt edici yapın (ör. filtre ve karbon farkı).
- **People Fun:** Okul ortamında "önce/sonra" bölge kartının ekran görüntüsü (F12) ve sınıf içi karşılaştırma, düşük maliyetli bir sosyal katman olabilir. **[Açık soru]**
- **Zorluk ayarı:** Tek bir zorluk var. **[Açık soru]** "Rahat" seçeneği (tehdit sayısı yarıya iner, ceza %10) acemi ve küçük yaştaki oyuncuyu korur (Chen 2007).
- **Bölge adlarıyla tehditleri eşleştirmek:** Yeni sade adlar (Fabrikalar, Fabrika Gölü) nedeni zaten söylüyor. Bu, keşif merakını azaltabilir ama erişimi artırır. Kullanıcı kararı; korunmalı.

## Eylem planı

1. **Hemen (düşük maliyet, yüksek etki):**
   - Gece uzunluğu ve başlangıç saati kararı.
   - Yarı dolu başlangıç bataryası ve gün batımı ipucu.
   - Hedef zincirinde P1'i büyüme süresine taşımak.
   - Ok kuralını aşamaya bağlamak.
   - Proje kartında tahmini malzeme.
   - GDD'yi yeni yapıya geçirmek.
2. **Playtest'ten sonra:**
   - Dönüşüm anının süresi ve şiddeti.
   - Plastik ve boru maliyeti.
   - Zorluk "Rahat" seçeneği.
   - Anket sorularının ayırt ediciliği.
3. **Orta vade:**
   - 3. kademe uzmanlık seçenekleri.
   - Çocuğun daha çok bağlama bağlı tepkisi.
   - Sınıf içi paylaşım.
4. **Kapsam:**
   - Hidroelektrik ve tesis 3. kademesi deneyim sütunlarına zayıf hizmet ediyor. Yeni iş eklemek yerine dondurulabilir (zaten uygulandı; cila önceliği düşük).
   - En yüksek cila önceliği: ilk 20 dakika ve dönüşüm anları.

## Playtest planı

**Katılımcılar:** 6–8 kişi.
- 3 kişi oyun deneyimi az olan lise ve üniversite öğrencisi.
- 3 kişi düzenli oyuncu.
- 1–2 kişi çevre bilgisi yüksek olan.

İlk 20 dakika yorumsuz izlenir, açıklama yapılmaz. Oturum 60 dakika sürer.

| Hipotez | Metrik | Başlangıç hedefi | Tutmazsa ne değişir |
|---|---|---|---|
| İlk büyük başarı yeterince erken | P1 tamamlanma süresi | ≤ 15 dk (oyuncuların %80'i) | Başlangıç saati ve batarya, zincir sırası |
| Gece engel değil, ders | İlk gecede şarj yanında boşta kalma; "neden?" sorusu | ≤ 60 sn; doğru açıklama ≥ %60 | Gün batımı ipucu, gece oranı |
| Ok erişimi çözüyor, keşfi öldürmüyor | P1 sonrası "Yerini göster" kullanımı; P2'ye başlama süresi | Kullanım ≤ %50; başlama ≤ 5 dk | Ok kuralı |
| Göl dönüşümü imza an | "Arkadaşına hangi anı anlatırdın?" | Oyuncuların ≥ %40'ı bir proje dönüşümünü anlatır | Dönüşüm anının cilası |
| Orta oyunda düzlük yok | P1 sonu → P2 kurulumu; boşta kalma oranı | ≤ 25 dk; boşta < %15 | Maliyet ve rehber metinleri |

**Anketler:**
- Oturum sonunda GEQ çekirdek modülü. Ana alt ölçekler: yeterlilik, akış, olumlu duygu, gerilim/rahatsızlık.
- PENS'in özerklik ve sezgisel kontroller alt ölçekleri.
- Oyun içinde, P1 öğrenme kartından sonraki doğal duraklamada GEQ oyun içi modülü.
- Mevcut ön ve son anket (v2), öğrenme farkı için.

**Kayıtlar:**
- İlk anlamlı eyleme kadar geçen süre.
- Her hedef adımının süresi.
- Boşta kalma (10 saniyeden uzun girdisizlik).
- Bayılma sayısı ve yeri.
- Oturumu bırakma anı.
- Gönüllü devam: 60. dakikadan sonra "devam etmek ister misin?".

## GDD'ye eklenecekler

- **Deneyim sütunları:** 4 sütun ve her birinin sınanabilir cümlesi.
- Hedef oyuncu ve oturum süresi.
- Faz başına duygu eğrisi ve gece ile gündüz ritmi.
- Gerçek hedef zinciriyle yazılmış onboarding planı.
- Hedef hiyerarşisi.
- Yönlendirme kuralının aşamalara bağlanması.
- Açık sorular listesi.
- Karar kaydı (tarih, gerekçe ve yerine geçtiği kural).
- Tek tabloda denge sayıları.
- Ölçüm kapıları ve playtest planı.
- Güncel uygulama özeti (ayrıntılı günlük arşivde).

Bu yapı `GDD_v4.md` olarak hazırlandı.
