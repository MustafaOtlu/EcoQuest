# Akış İncelemesi: EcoQuest GDD v4 (ikinci tur)

> **Kapsam ve yöntem:** `GDD_v4.md` ilk incelemenin (`GDD_v3_Inceleme.md`) önerileriyle yazıldıktan sonra aynı ölçütlerle yeniden okundu. Ölçütler: katman puanları, M0 belge haritası, M1 ilk oturum, M2 tutarlılık, M3 sayılar, M12 imza an, M14 playtest. v4'teki yeni iddialar kodla karşılaştırıldı (`EcoEnemySpawnZone`, `EcoEveningLighting`, `EcoQuestHUD`, `EcoPerf`). Bu incelemede bulunan sorunların belge içinde düzeltilebilenleri aynı gün v4'e işlendi; aşağıda "düzeltildi" diye işaretli. Masa başı incelemedir.


> **Düzeltme (27 Ekim, uygulama sırasında):** Bu raporun "ilk gece P1'i kilitliyor" bulgusu yanlıştı. Kod okunurken görüldü ki üste hazır bir güneş paneli, **rüzgâr türbini**, 80 EB'lik batarya ve şarj istasyonu var ve P1 bu ağı sayıyor. Yani P1 gece de tamamlanabiliyordu. Gece ve gündüz oranı ile ilk gün saati yine de kullanıcı kararıyla değiştirildi (S1). Önerilen düzeltmeler de uygulandı, ama farklı gerekçeyle. İlk oturumdaki gerçek sorunlar şunlardı:
> - Yön oku P1 için Güneş Ovası'nın ortasını gösteriyordu.
> - Proje adımı ancak hasattan sonra çıkıyordu.
> - Kart somut işi ve tuşu söylemiyordu.
>
> Ayrıntı ve güncel durum: `GDD_v4.md` B bölümü ve §21.

## Kısa hüküm

v4, v3'ün **belge olarak** en büyük açıklarını kapatıyor:
- Deneyim hedefi ve dört sütun yazılı; özellikler sütunlara bağlı.
- İlk oturum, oyundaki gerçek hedef zinciri ve saatleriyle anlatılıyor.
- Tempo ve duygu eğrisi var.
- Açık sorular, bekleyen öneriler ve karar kaydı ayrı.
- Ölçüm planı, sınanabilir cümlelerle eşleşiyor.

**Oyun olarak** ise değişen bir şey yok. v3 incelemesinin bulduğu ilk oturum sorunu (ilk gece P1'i kilitliyor) v4'te doğru anlatılıyor, ama hâlâ oyunda. Oyuncuların nerede bırakacağı tahmini aynı: **6.–13. dakikalar** ve **P1 → P2 geçişi**. Bu iki yer, Ö1–Ö5 uygulanmadan playtest'e girilirse büyük olasılıkla ilk playtest'in ana bulgusu olur.

| # | Sorun (v4'te kalan) | Katman | Durum |
|---|---|---|---|
| 1 | İlk oturumun sorunu belgelendi ama oyunda duruyor; gece oranı kararı bekliyor (S1) | K0/K2 | Kullanıcı kararı + Ö1–Ö3 |
| 2 | İmza an adayı "ilk gece", oyunda olmayan bir mekaniğe dayanıyordu (ışıklar enerjiye bağlı değil) | K5 | Düzeltildi: öneri olarak yeniden yazıldı |
| 3 | Hedef kartı proje adımını yalnız elde ürün varken gösteriyor; malzeme eksikliği söylenmiyor | K3 | Ö5'e eklenmeli (aşağıda) |
| 4 | Ölçüm planının süreleri kaydedecek bir araç yok | Ölçüm | Düzeltildi: Ö6 olarak eklendi |
| 5 | Öğrenme hipotezi tavan etkisiyle çelişiyordu (ön anket %80 üstündeyse +%20 imkânsız) | Ölçüm | Düzeltildi |

## Katman puanları (belge olarak, v3 → v4)

| Katman | v3 | v4 | Gerekçe |
|---|---|---|---|
| K0 Erişim | 3 | 4 | İlk oturum gerçek zincirle yazıldı (B); yönlendirme kuralı tek ve aşamalı (§6, S2). Sorun oyunda sürdüğü için 5 değil |
| K1 Anlık döngü | 4 | 4 | Değişmedi; proje bitişi geri bildirim tablosuna eklendi (§5) |
| K2 Zorluk ve tempo | 2 | 3 | Faz ve duygu tablosu, gece ritmi, dönüşüm anı tanımlı (B). Gece oranı ve zorluk seçeneği hâlâ açık soru (S1, S3) |
| K3 Hedef ve ilerleme | 4 | 4 | Hedef hiyerarşisi tablosu ve P1 → P2 hesabı eklendi. Uzmanlıktaki seçimsizlik hâlâ açık (S4) |
| K4 Merak ve keşif | 3 | 3 | Ok aşama kuralı keşfi koruyor (S2); ama yeni bir merak kaynağı eklenmedi |
| K5 Anlam ve bağ | 4 | 4 | Sütun 3 ve imza an açık. People Fun hâlâ yok (S6) |

## Bu turda bulunan ve düzeltilen hatalar

1. **İmza an iddiası yanlıştı (M12).**
   - v4'ün ilk hâli "ilk gece: kendi kurduğu batarya ile ışıkların yanması" diyordu.
   - `EcoEveningLighting` sokak lambalarını, pencereleri ve şarj istasyonu ışığını saatle yakıyor; ağda enerji olup olmaması fark etmiyor. Yani oyuncu bataryanın gece işe yaradığını **göremez**.
   - v4'te bu yer, mevcut durum ve öneri olarak yeniden yazıldı: oyuncunun kurduğu şarj istasyonunun ışığı yalnız enerji varken yansın.
   - Bu öneri Ö1–Ö2 ile birlikte uygulanırsa ilk gece hem ders hem duygu taşır (Sütun 2).
2. **Öğrenme hipotezi kendi tavan uyarısıyla çelişiyordu (M2).** §16 tavan riskini (ön test %80 üstü) yazıyor, §18 ise "+%20" istiyordu. Hipotez iki koşullu hâle getirildi.
3. **Ölçüm planı ölçülemezdi (M14).** Oyunda yalnız performans CSV'si var (`EcoPerf`); P1 süresi, boşta kalma ve hedef adım süreleri kaydedilmiyor. Ö6 eklendi: yerel olay günlüğü. Yoksa süreler kronometreyle tutulur.
4. **Hedef oyuncu bir varsayımdı.** "14 yaş üstü" bilgisi kullanıcıdan gelmedi. "Onay bekliyor" diye işaretlendi; S9 açık sorusu eklendi.
5. **Öneriler dağınıktı (M0).** Önerilerin bir kısmı bölüm içlerine dağılmıştı (B, §6, §7). §19'a tek bir "Bekleyen öneriler" tablosu (Ö1–Ö6) eklendi.
6. **Gelecek kayıtların yeri belirsizdi.** v3 kuralı "her değişiklikte uygulama tablosuna satır" diyordu, ama v4'te hangi tabloya yazılacağı belli değildi. §21'e "Değişiklik günlüğü" eklendi. v3 arşiv olarak değiştirilmeden kalıyor.

## Doğrulanan iddialar (kodla)

| v4 iddiası | Kaynak | Sonuç |
|---|---|---|
| Gün 900 sn, başlangıç 09:00, güneş 06:00–18:00 | `EcoWorldClock`: `hour = 9`, `dayLengthSeconds = 900`, `SolarFactor = sin((saat−6)·π/12)` | Doğru |
| Gece tehdit doğuşu artmaz | `EcoEnemySpawnZone`: saat yalnız gün sayacı ve yağmur için kullanılıyor | Doğru |
| Ganimet değerleri | `EnemyBrain.SpawnLoot`: Teneke 6M, Salya 5P, Asit 3P, Duman ve Alev 2P | Doğru |
| Kamera ortografik, 45°/50°, 4–10 (5,5) | `EcoIsoCamera` | Doğru |
| Hedef kartı önceliği | `EcoQuestHUD.RefreshObjective` | Doğru; ama aşağıdaki bulguya bakın |

## Kalan bulgular

### 1. Proje adımı ürüne bağlı, malzemeye değil (K3)

- **Kodda ne var:** `EcoQuestHUD.RefreshObjective` açık projeyi yalnız `weapons.Food > 0` iken gösteriyor.
- **Ne oluyor:** Oyuncu P2 malzemesi için bütün ürünlerini markette satarsa ürünü sıfıra iner. Bu durumda hedef kartı projeyi göstermeyi bırakır ve "Bitkin büyüyor · atık topla"ya döner. Kart ne eksik olduğunu (plastik) ya da nereden bulunacağını söylemez.
- **Neden sorun:** Oyuncu hedefe ne kadar yakın olduğunu bilmeli (GameFlow). Görünmeyen bir kural ("proje ancak ürün varken gösterilir") akış bozucudur.
- **Öneri [Karar içinde], Ö5'e ek:** Hedef kartı açık projeyi her zaman göstersin. Malzeme yetmiyorsa ikinci satırı eksiği ve kaynağı söylesin ("Proje: Gölü canlandır · 62 plastik eksik · Salya 5, hasat 1–2").
- **Ölçüm:** P1 → P2 aralığında "ne yapacağımı bilmiyordum" anları; hedef kartının proje adımında kalma süresi.

### 2. İlk oturum hâlâ oyunda bozuk (K0/K2)

v4 sorunu doğru anlatıyor, ama ancak S1 kararı ve Ö1–Ö3 uygulanınca çözülür. Playtest bunlardan **önce** yapılırsa P1 süresi hipotezi (≤ 15 dk) büyük olasılıkla tutmaz. Bu sonuç önceden bilinen bir şeyi ölçmüş olur. **Sıralama önerisi:** S1 kararı → Ö1, Ö2, Ö3, Ö6 → ilk playtest. Ö4 (dönüşüm anı) ve Ö5 ikinci tura kalabilir.

### 3. Yeni bir merak kaynağı yok (K4)

v4 keşfi korumak için oku sınırlıyor (S2), ama merakı **artıran** bir öğe eklemiyor.
- **[Açık soru]** Düşük maliyetli bir aday: bölge kartında henüz taranmamış bölgenin gerçek ölçüleri "?" olarak görünsün. İlk taramada değerler açılsın (Malone: gizli bilginin kademeli açılması).
- **Bedeli:** 27 Ekim'de istenen "gerçek veriler haritada görünsün" isteğiyle kısmen çatışır. Bu yüzden kullanıcıya bırakılır.

### 4. Okunabilirlik (belge)

- v4 v3'ten kısa (≈ 1000 satır, ~80 KB). Bunun nedeni 100 satırlık günlüğün arşivde kalması.
- Sistem bölümleri (§1–§17) v3 numaralarını koruyor. Bu, eski göndermeleri ve koddaki "GDD §n" yorumlarını geçerli tutuyor.
- Bir sonraki sürümde bölümler yeniden numaralanmamalı. Yeni bölümler harfle (C, D) ya da §22'den sonra eklenmeli.

## Eylem planı

1. **Kullanıcı kararı:**
   - S1 (gece oranı ve başlangıç saati).
   - S2 (ok kuralı).
   - Ö1–Ö6 onayı.
   - S7 (fener), S8 (imleç), S9 (hedef oyuncu).
2. **Hemen (onay sonrası):**
   - Ö1 batarya.
   - Ö2 gün batımı ipucu ve fener tanıtımı.
   - Ö3 zincir sırası.
   - Ö6 olay günlüğü.
   - Ö5 hedef kartında eksik malzeme.
3. **İlk playtest:** §18 planı, 6–8 kişi.
4. **Playtest'ten sonra:**
   - Ö4 dönüşüm anı.
   - Plastik ve boru maliyeti.
   - S3 zorluk seçeneği.
   - S4 uzmanlık seçimi.
   - Anket ayırt ediciliği.

## Playtest planı

v4 §18 geçerlidir; bu tur yalnız iki değişiklik getirdi: öğrenme hipotezi düzeltildi ve kayıt aracı (Ö6) eklendi.
