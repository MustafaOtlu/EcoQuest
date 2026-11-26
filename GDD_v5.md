# ECOQUEST
## Oyun Tasarım Belgesi · v5 (roguelite run yapısı)

> **Bugünkü çevresel kararların, gelecek nesillerin yaşam koşullarını belirler.**
>
> Her oyunda farklı bir vadi: 5 oyun gününde vadinin sorunlarını çöz, notunu al, yeşil krediyle bir sonraki vadiye daha hazırlıklı gir.

| Belge bilgisi | Değer |
| --- | --- |
| Güncelleme | 26 Kasım 2026 |
| Proje / ekip | TÜBİTAK EcoQuest · Mustafa, Hanefi, Bilge |
| Platform | Windows PC · klavye ve fare · tek oyuncu |
| Motor / oyun sahnesi | Unity 6000.0.74f1 · `Assets/Scenes/EcoWorld.unity` |
| Deneyim | Kısa ve tekrar oynanabilir çevre iyileştirme run'ları (≈60–75 dk) + run'lar arası kalıcı ilerleme |
| Önceki sürüm | `GDD_v4.md`. v4'ün §5–§16 sistem kuralları (oyun hissi, araçlar, inşa, ekonomi, göstergeler, final, arayüz, görsel/ses, eğitsel hedefler) **aynen geçerlidir**; bu belge yalnız değişen yapıyı anlatır |
| Arşiv | `GDD.md` (v3) değiştirilmez. v4'ün değişiklik günlüğü 26 Kasım Aşama 2 satırında kapanır; yeni satırlar bu belgenin §21'ine yazılır |

**Neden v5?** v4 tek ve sabit bir harita üzerinde 3–5 saatlik tek bir hikâyeydi. Bitince tekrar oynamak için sebep yoktu; kaybetme ve zaman baskısı yoktu; ekosistem sağlığı yalnız gösteriliyordu. v5'te oyun, her biri farklı bir vadide geçen run'lardan oluşur (R.E.P.O benzeri: her girişte farklı harita, farklı sorunlar, sorunlara göre değişen hedefler).

Etiketler v4 ile aynıdır: **Karar**, **Tasarım başlangıcı**, **Denge taslağı**, **Öneri**. **Uygulandı** demek kodda var ve en az bir kez oyunda denendi demektir; gerçek oyuncu testinin yerini tutmaz.

### İçindekiler

1. [v4'ten v5'e ne değişti](#degisen)
2. [Run döngüsü](#run)
3. [Vadi: karıştırılan karolar](#vadi)
4. [Hedefler, sabah kartları, akşam krizleri](#icerik)
5. [Meta ilerleme: yeşil kredi ve açılım ağacı](#meta)
6. [Denge ve ölçüm](#denge)
7. [Kayıt](#kayit)
8. [Açık sorular ve riskler](#acik)
9. [Kod haritası](#kod)
21. [Değişiklik günlüğü](#gunluk)

---

<a id="degisen"></a>
## 1. v4'ten v5'e ne değişti

| Konu | v4 | v5 |
| --- | --- | --- |
| Harita | Tek sabit harita, editörde bir kez üretilir | 3×3 karo vadi; dört yan karo her run'da zarlanır ve döndürülür (§3) |
| Oturum | 3–5 saatlik tek hikâye, finalden sonra serbest oyun | 5 oyun günlük run (gün 15 dk), sonunda not (§2) |
| Hedef | 6 sabit proje | Vadide olan bölgelerden 2 zorunlu + 1 bonus hedef (§4) |
| Kaybetme | Yok | Sağlık iki sabah üst üste %25'in altında → vadi çöker; zorunlu hedef yoksa başarısız (§2) |
| Günlük olaylar | Yok | Sabah 3 kart (bazılarının bedeli var), akşam 18:00 kriz (§4) |
| Final | %50 hazırlık kapısı | 5. gün fırtına; canavarın gücü vadinin sağlığına bağlı |
| Ekosistem sağlığı | Yalnız HUD'da | Gerçek skor: bölgelerin ortalamasının yarısı + en kötü bölgenin yarısı |
| İlerleme | Run içi YEP ve uzmanlık (her şey alınabiliyordu, S4) | YEP run içinde kalır; uzmanlık dalları ve 3. kademe yeşil krediyle açılır (§5) |
| Eğitim | Her oyunun başında | İlk run sabit "Eğitim Vadisi"; sonra atlanabilir |

**Karar (kullanıcı):** harita karıştırılan bölge karolarından; run 5 oyun günü ve notla biter; run'lar arasında yeşil krediyle kalıcı açılımlar; her sabah 3 karttan 1'i seçilir.

<a id="run"></a>
## 2. Run döngüsü

```
Başlık "Yeni oyun" → brifing (vadi, ana sorun, bölgeler, 3 hedef, zorluk, açılım ağacı)
  → "Vadiye in": sahne bu vadinin karolarıyla yeniden kurulur
  └─ Gün 1..5 (15 dk): sabah kart seç → keşfet, topla, dik, kur, bağla → tehditleri yönet
                      → 18:00 kriz → gece
  └─ 5. gün: fırtına terminali açılır (İklim Canavarı, gücü kalan kirliliğe bağlı)
  └─ 6. gün şafağı: değerlendirme → not (S/A/B/C) ya da başarısız/çöktü → yeşil kredi
  → "Yeni vadi" (sahne yeniden yüklenir, doğrudan brifing) ya da "Vadide kal"
```

- **Puan (Denge taslağı):** zorunlu hedef 30, bonus 15, sağlık × 0,25, fırtına zaferi 10; en çok 100. S ≥ 90, A ≥ 75, B ≥ 60, sonrası C.
- **Kaybetme:** sağlık iki sabah üst üste %25'in altında → "Vadi çöktü". 5 gün bitince hiç zorunlu hedef tamamlanmamışsa → "Hedefler tutmadı".
- **Zorluk:** Sakin ×0,85 / Normal ×1 / Kriz ×1,2 (kirlilik ve kriz gücü); Kriz açılımla gelir ve 1,4× kredi verir. Zorluk hedef seçimini değil yalnız eşikleri değiştirir.
- **Senaryo zarları (`EcoScenario`):** vadinin adı, ana sorunu (kirli göl, duman, yangın, atık, ısınma), bölge başına kirlilik ×0,75–1,3 ve örtü ×0,8–1,15, bir sürpriz kirli bölge (+10–24), başlangıç yangınları, 4–15 atık yığını, güneş ve rüzgâr verimi ×0,85–1,15, 0–15 başlangıç metali/plastiği.
- **Eğitim Vadisi:** ilk run; sabit tohum, eski haritanın düzeni (batıda Balık Gölü, doğuda Fabrika Gölü, üstte Orman ve Fabrikalar), hiç döndürme yok; hedefler enerji projesi + göl projesi + bonus atık.

<a id="vadi"></a>
## 3. Vadi: karıştırılan karolar

Vadi 150 × 150 m'dir ve 50 m'lik 3 × 3 karodan kurulur.

| Sıra | Batı | Orta | Doğu |
| --- | --- | --- | --- |
| Üst | Buzul Dağı (sabit, 150 × 50 m) | ← | ← |
| Orta | **yuva: batı** | Şehir (sabit) | **yuva: doğu** |
| Alt | **yuva: güneybatı** | Güneş Ovası / üs (sabit) | **yuva: güneydoğu** |

- **Yan karolar:** Fabrika Gölü ve Fabrikalar her vadide vardır. Balık Gölü, Rüzgâr Tepeleri ve Orman'dan biri dışarıda kalır. Dört karo yuvalara karıştırılır ve her biri 0°/90°/180°/270° döndürülür (`EcoValley`). 3 dışarıda kalan × 24 sıra × 256 dönüş = 18 432 farklı vadi.
- **Dikiş kuralları:** yan karoların kenarlarında zemin yüksekliği ortak vadi tabanına sabitlenir (orta ve alt sırada düz), kenar rengi ortak çimen tonuna geçer, her kenarın ortasında 8 m'lik bir yol ağzı vardır. Bu yüzden her karo her yuvaya her dönüşle oturur. Harita duvarına bakan yol ağızları oyunda gizlenir, haritada yanındaki zeminle örtülür.
- **Üretim (editör):** `EcoQuest/Valley/Build region tiles` mevcut harita üreticisinin karo kipiyle 9 prefab (`Assets/Resources/EcoTiles/`), mesh'leri (`Assets/EcoTiles/Meshes/`) ve her karonun yukarıdan harita görüntüsünü üretir (≈20 sn). `Switch scene to tile valley` sahneyi karolu vadiye geçirir.
- **Kurulum (oyun):** `EcoValleyAssembler` her şeyden önce (Awake, yürütme sırası −32000) karoları yerine koyar ve döndürür, dış yol ağızlarını kapatır, kirlilik alanını ekler, minimapi karo görüntülerinden birleştirir (dönüşlerle birlikte), başlangıç tabelasını yazar ve oyuncuyu üs patikasına koyar. NavMesh, düşman bölgeleri, projeler ve kayıt hazır dünyayı bulur.
- **Şehir karosu** eski şehirden küçüktür: göbekli kavşak, 4 kamu yapısı, 6 küçük ev.

<a id="icerik"></a>
## 4. Hedefler, sabah kartları, akşam krizleri

### Hedefler (15; eşikler Sakin / Normal / Kriz)

Run başında vadide olan bölge türlerinden seçilir: 2 zorunlu + 1 bonus, aynı gruptan ikisi gelmez, ilk hedef ana soruna sıkı bağlıdır (ağırlığın karesi). Sayısal hedefler 120 sn tutulunca kalıcı tamamlanır.

| Kimlik | Grup | Bölge | Ölçü | Eşik |
| --- | --- | --- | --- | --- |
| lake_project | göl | Göl | Göl projesi | — |
| lake_oxygen | göl | Göl | Çözünmüş oksijen ≥ | 6,5 / 7 / 7,5 mg/L |
| fish_stock | göl | Göl | Ortalama balık stoku ≥ | 6 / 8 / 9 |
| industry_aqi | sanayi | Sanayi | AQI ≤ | 90 / 70 / 55 |
| industry_project | sanayi | Sanayi | Sanayi projesi | — |
| grove_cover | orman | Orman | Örtü ve habitat ≥, yangın yok | %50 / 60 / 65 |
| world_carbon | iklim | Buzul | Vadi CO₂ ≤ | 485 / 475 / 468 ppm |
| glacier_project | iklim | Buzul | Buzul projesi | — |
| clean_power | enerji | Güneş | Oyuncunun kurduğu temiz üretici ≥ | 3 / 4 / 5 |
| **wind_farm** (yeni) | enerji | Rüzgâr | Rüzgâr Tepeleri'nde oyuncunun türbini ≥ | 1 / 2 / 3 |
| resilience_project | enerji | Şehir | Dayanıklılık projesi | — |
| waste_cleared | atık | Şehir | Kalan atık yığını ≤ | 3 / 1 / 0 |
| **tree_planting** (yeni) | ağaç | Güneş (her vadide) | Oyuncunun diktiği, sağlıklı ve en az yarı büyümüş ağaç ≥ | 6 / 10 / 14 |
| valley_health | sağlık | Şehir | Ekosistem sağlığı ≥ | %55 / 62 / 70 |
| energy_project | enerji | Güneş | Enerji projesi (yalnız eğitim) | — |

Rüzgâr Tepeleri'nin artık kendi hedefi vardır (Aşama 2'de bu karonun hiçbir hedefi yoktu).

**Vadiye göre sıkılaştırma (Tasarım başlangıcı):** vadiye inildiğinde zarlanan değerler dünyaya işlenir ve ölçülen hedefler, vadinin başlangıcından belli bir adım daha iyisini isteyecek şekilde ayarlanır; katalogdakinden hiçbir zaman kolay olmaz. Karbon ve sanayi AQI: başlangıcın %80 / 70 / 60'ı (Sakin / Normal / Kriz). Sağlık: başlangıç + 8 / 12 / 16 puan (en çok %90). Göl oksijeni: başlangıç + 1 / 1,5 / 2 mg/L (en çok 9). Orman örtüsü: başlangıç + 0,16 / 0,20 / 0,24 (en çok 0,9; Fidanlar kartının +0,12'si tek başına yetmez). Brifing bunu tek satırla söyler; hedef paneli gerçek eşiği gösterir.

Aşama 3 sırasında denenen "Şehrin havası" (şehir AQI) hedefi çıkarıldı: şehirde kalıcı bir kirlilik kaynağı yok, havası oyuncu hiçbir şey yapmasa da temizleniyordu (denge sondası, §6).

### Sabah kartları (19)

Her sabah 3 kart (açılımla 4), biri alınır. Çekiliş tohum ve güne bağlıdır; ana soruna uyan kartın ağırlığı 2,2×. "Bir kez" kartları run'da bir kez gelir.

| Tür | Kartlar |
| --- | --- |
| Hibe | Belediye hibesi, Geri dönüşüm fonu, Kompost kooperatifi, Fidanlık (yeni: +12 ağaç tohumu) |
| Politika | Bisiklet yolları*, Yeşil çatılar* (yeni), Karbon vergisi (yeni, bedel 15 metal), Gece devriyesi* (yeni: her bölgede günde 2 tehdit az) |
| Teknoloji / doğa | Rüzgâr ölçümü*, Panel temizliği*, Bilim ekibi*, Fidan kampanyası, Kıyı sazlıkları, Dere temizliği (yeni), Göl tabanı taraması (bedel 20 metal), Gönüllü temizlik, Yangın ekibi |
| Bedelli kazanç | Fabrika fazla mesaisi (sanayi kirlenir), Hurda müzayedesi (yeni: +25/+25, 3 yeni atık yığını) |

\* bir kez alınır.

### Akşam krizleri (10)

18:00'de bir kriz; dünkü kriz tekrar gelmez; ana soruna uyan kriz 2,5× ağırlıklı. Güç = (0,6 + 0,25 × gün) × zorluk. Hazırlık etkiyi azaltır.

| Kriz | Etki | Hazırlık |
| --- | --- | --- |
| Kuru yaz yangını | Orman, rüzgâr ve ovada yangın | Yangın ekibi kartı |
| Petrol sızıntısı | Gölde su kirliliği, balık zehirlenir | — |
| Sıcak hava dalgası | Depo ve arıtma suyu, bitki nemi, örtü düşer | Kapalı depo, sulama |
| Fabrika kaçağı | Sanayide hava, su, toprak, karbon | Filtreli fabrikalar |
| Balçık göçü | Düşman bütçesi artar | — |
| Asit yağmuru | Her bölgede su ve toprak | Bitki örtüsü |
| Kaçak döküm | Şehir, ova, sanayide atık yığınları | — |
| **Sel baskını** (yeni) | Göllerde su ve toprak, kıyıya atık | Kıyı örtüsü |
| **Sıcaklık terslenmesi** (yeni) | Şehirde ve sanayide hava | Filtreler, şehir yeşili |
| **İstilacı tür** (yeni) | Balık stoku düşer | Oksijeni ≥ 7 mg/L göller |

<a id="meta"></a>
## 5. Meta ilerleme: yeşil kredi ve açılım ağacı

Profil ayrı bir dosyadadır (`<persistentDataPath>/EcoQuest/meta.json`); bir run kaydını yüklemek ya da kaybetmek onu etkilemez.

**Kredi (Denge taslağı):** eğitim 30 (başarısız 10); başarılı run S 60 / A 45 / B 30 / C 20, başarısız 8; her bonus hedef +5, fırtına zaferi +10; Sakin ×0,8, Kriz ×1,4.

**Açılım ağacı (11 düğüm, toplam 515 kredi).** Derin bir düğüm, aynı daldaki "Önce" düğümlerinden **birini** ister. Brifingde ve değerlendirme ekranında "Açılım ağacı" düğmesiyle açılır.

| Dal | Düğüm | Kredi | Önce | Etki |
| --- | --- | --- | --- | --- |
| Başlangıç | Başlangıç hibesi | 30 | — | Her run +20 metal, +20 plastik |
| Başlangıç | Tohum bankası | 35 | — | Her run +10 ağaç tohumu |
| Başlangıç | Su planları | 45 | Başlangıç hibesi | Pompa, arıtma, depo tarifleri baştan açık |
| Başlangıç | Verimli aletler | 50 | Hibe ya da tohum bankası | Alet elektriği %15 az |
| Uzmanlık | Su dalı | 40 | — | [K] uzmanlıkta Su dalı |
| Uzmanlık | Ekoloji dalı | 40 | — | [K] uzmanlıkta Ekoloji dalı |
| Uzmanlık | Usta | 80 | Su ya da Ekoloji dalı | Dallar 3. kademeye çıkar (yoksa en çok 2) |
| Sabah ve kriz | Erken uyarı | 40 | — | Akşam krizi öğleden haber verilir |
| Sabah ve kriz | Kart yenileme | 45 | — | Günde bir kez sabah kartlarını yenile |
| Sabah ve kriz | Geniş seçenek | 60 | Yenileme ya da erken uyarı | Sabah 4 kart |
| Zorluk | Kriz zorluğu | 50 | — | Kriz zorluğu seçilebilir, 1,4× kredi |

**Uzmanlık bağlantısı (v4 S4'ün çözümü):** Enerji dalı herkese açıktır; Su ve Ekoloji dalları ve 3. kademe meta açılımıdır. Böylece bir run'da her şey alınamaz, her run'ın uzmanlık seçimi anlam kazanır. YEP ve hak kazanımı (her 40 YEP bir hak) v4 §7 ile aynıdır.

**Tempo hesabı:** başarılı bir Normal run ortalama 35–45 kredi getirir; ağacın tamamı ≈ 12–14 run'da (≈ 13–16 saat) dolar. İlk iki run'da ilk açılım alınır.

<a id="denge"></a>
## 6. Denge ve ölçüm

- **Run günlüğü:** biten her run `<persistentDataPath>/playtest/runs.csv`'ye bir satır yazar: tarih, vadi adı, vadi kodu (karolar ve dönüşler), tohum, zorluk, ana sorun, sonuç, not, puan, kredi, oynanan dakika, tamamlanan hedefler, seçilen kartlar, krizler, günlük sağlık. Oturum günlüğüne de `run_bitti` olayı düşer. Playtest'te not dağılımı, run süresi ve hangi hedefin hiç tutmadığı buradan okunur.
- **Doğal toparlanma (Denge taslağı):** zarlanan vadilerde bölgelerin kendiliğinden temizlenmesi v4 hızının 0,35'idir (`EcoRun.NaturalRecovery`); Eğitim Vadisi v4 hızında kalır. Bilim ekibi kartı ve Ekoloji dalı bunu çarpar. v4 hızında, boşta duran oyuncunun vadisi 5 günde kendiliğinden iyileşiyordu (sağlık 49 → 58, karbon 20 → 12). Aynı kural vadinin kendi (yabani) ağaçlarına da uygulanır: zarlanan vadide yabani ağaçların temizleme ve karbon emme etkisi 0,35² ≈ 0,12'ye iner ve orman örtüsünü yalnızca vadinin başlangıç örtüsüne kadar geri büyütebilirler (`EcoRegion.wildCover`). Oyuncunun diktiği ağaçlar tam güçte çalışır ve örtüyü %100'e kadar artırır. Ölçülen hedefler ayrıca vadinin başlangıç durumuna göre sıkılaştırılır (§4, `EcoRun.TightenGoals`).
- **Denge sondası (yalnız editör):** `EcoBalanceProbe` bir run'ı boşta duran oyuncu gibi hızlandırılmış oynar (ilk kartı alır, krizleri kapatır, hiçbir şey kurmaz) ve her gün için sağlık, karbon, kriz sonucu ve atık sayısını `BALANCE_PROBE` satırı olarak yazar. Amaç: hiçbir şey yapmayan oyuncu hiçbir zaman başarılı olmamalı, ama 5 günden önce de çökmemeli (oyuncuya toparlanma süresi).
- **Sonda sonucu (26 Kasım):** Tohum 198 (Grove3-Lake1-DirtyLake1-Industry1, Normal, ısınma; hedefler: dünya karbonu ≤ 14, vadi sağlığı ≥ 63, orman örtüsü ≥ %60), boşta oyuncu, ilk kart her sabah (Bilim ekibi dahil). Üç tur: (1) yalnız doğal toparlanma ×0,35 → **Başarılı C, 58 puan**; karbon 19,5 → 13,2'ye kendiliğinden indi, örtü yabani ağaçlarla büyüdü (karbon ve örtü hedefleri boşta tuttu). (2) yabani ağaçlar da ×0,35 → yine C, 57 puan (ağaç sayısı çok olduğu için yetmedi). (3) yabani ağaç emme ×0,12 + başlangıç örtüsü tavanı + örtü eşiği +0,04 → **Başarısız, 12 puan, 0 hedef**; günlük sağlık 44/46/48/49/46 (çöküş sınırı 25'in hayli üstünde), karbon kriz sonrası 25,8'den 15,9'a iner ama eşiğe varmaz. Ayrıca tohum 225290109 (kirli göl) Başarısız 14 puan; tohum 64'te boşta tutan `city_air` hedefi (şehirde kirletici yok) havuzdan çıkarıldı. Sonuç: hiçbir şey yapmayan oyuncu kaybediyor ama vadi 5 gün ayakta kalıyor. Sıradaki ölçüm: gerçek oyuncu run'larında `runs.csv` not dağılımı (hedef: ilk run'ların çoğu C/B, S nadir).

<a id="kayit"></a>
## 7. Kayıt

- Kayıtlar vadi kodunu saklar (`Grove1-DirtyLake-Industry-Wind3`: yuva sırasıyla karo + dönüş). Başka bir vadinin kaydı yüklenince sahne o vadiyle yeniden kurulur, kayıt sonra açılır. Aşama 2'de yazılan dönüşsüz kodlar dönüş 0 olarak okunur.
- Run durumu (tohum, gün, hedefler, kartlar, krizler, yenileme günü, devriye, oynanan süre) global kanca olarak saklanır.
- Eski sabit haritanın kayıtları açılmaz; slot listesinde "Eski sabit harita kaydı" yazar.
- Meta profili ayrı dosyadadır, run kaydıyla birlikte geri alınmaz.

<a id="acik"></a>
## 8. Açık sorular ve riskler

| No | Soru / risk | Durum |
| --- | --- | --- |
| V1 | Karo başına ikinci bir tasarım (farklı göl biçimi, farklı fabrika dizilişi) gerekli mi, yoksa dönüş yeterli mi? | Açık; playtest'te "vadiler birbirine benziyor" denirse yapılır |
| V2 | Şehir karosu küçük; ev sayısı yetiyor mu? | Açık |
| V3 | Kredi temposu (ağacın tamamı ≈ 13 run) uzun mu? | runs.csv ile ölçülecek |
| V4 | Kriz gücü 5. günde ×1,85 (Kriz zorluğunda ×2,2): fazla mı? | Sonda ve playtest |
| V5 | Genel arayüz kullanıcının şablonunu bekliyor (v4'ten devam) | Bekliyor |
| V6 | v4'ten açık kalanlar: S7 (fener), S8, Ö4 kıyı kurulum alanı | Bekliyor |

<a id="kod"></a>
## 9. Kod haritası

| Dosya | Görev |
| --- | --- |
| `Run/EcoRun.cs` | Run durumu, gün döngüsü, hedef ölçümü, puan, kredi, run günlüğü, kart yenileme |
| `Run/EcoScenario.cs` | Tohumdan senaryo zarları |
| `Run/EcoGoal.cs` | Hedef kataloğu ve seçimi |
| `Run/EcoDayCards.cs`, `Run/EcoCrisis.cs` | Kart ve kriz havuzları ve etkileri |
| `Run/EcoMeta.cs` | Profil, açılım ağacı, uzmanlık kilitleri |
| `Run/EcoRunUI.cs` | Brifing, kartlar, kriz, değerlendirme, hedef paneli, açılım ağacı |
| `Run/EcoValley.cs` | Karo yerleşimi ve dönüşleri, vadi kodu |
| `Run/EcoValleyAssembler.cs` | Vadiyi oyunda kurar, minimap |
| `Run/EcoBalanceProbe.cs` | Editörde denge sondası |
| `Editor/BuildEcoValleyTiles.cs` | Karo prefablarını ve haritalarını üretir |
| `World/EcoSpecialization.cs` | Uzmanlık (dallar meta açılımına bağlı) |
| `UI/EcoPlaytestLog.cs` | Oturum günlüğü ve `runs.csv` |

<a id="gunluk"></a>
## 21. Değişiklik günlüğü

| Tarih | Madde | Durum |
| --- | --- | --- |
| 26 Kasım | **Rastgele vadi run'ları, Aşama 3 (meta, içerik, denge).** **Karo dönüşleri:** yan karolar tohumla 0/90/180/270° döner; vadi kodu dönüşü rakamla taşır (`Grove1-DirtyLake-Industry-Wind3`, rakamsız eski kodlar 0° sayılır), minimap karoları da döndürür, dışa bakan 6 yol ağzı haritada zeminle örtülür. **Açılım ağacı (§5):** 4 grup (Başlangıç, Uzmanlık, Sabah ve kriz, Zorluk), 11 açılım, toplam 515 kredi, iki seviyeli ön koşullar; yeni açılımlar: Planlar (su tarifi baştan açık), Verimli aletler (enerji kullanımı ×0,85), Su/Ekoloji dalı, Uzman (uzmanlık 3. kademe), Kart yenileme, Erken uyarı, 4. kart, Kriz zorluğu. Uzmanlık dalları ve 3. kademe artık meta açılımına bağlı (Enerji hep açık). Sonuç ekranından ve brifingden açılan 4 sütunlu ağaç ekranı. **İçerik:** 6 yeni sabah kartı (toplam 19: fidanlık, dere temizliği, yeşil çatılar, karbon vergisi, gece devriyesi, hurda mezadı), 3 yeni kriz (toplam 10: sel, sıcaklık terslemesi, istilacı tür; aynı kriz üst üste gelmez), 2 yeni hedef (rüzgâr çiftliği yalnız Rüzgâr Tepeleri olan vadide, ağaçlandırma: büyüyen dikili ağaç sayısı); boşta tutan `city_air` çıkarıldı. Günde bir kez kart yenileme. **Denge (§6):** zarlanan vadide doğal toparlanma ×0,35, yabani ağaçların emmesi ×0,12 ve örtü büyümesi başlangıç örtüsüyle sınırlı; ölçülen hedef eşikleri vadinin başlangıç değerine göre sıkılaştırılıyor; brifing bunu söylüyor. `runs.csv` run başına bir satır; editörde `EcoBalanceProbe` boşta oyuncu sondası. **Doğrulama:** EditMode 18/18 (yeni `EcoMetaTests`: ağaç yapısı, kart/kriz kimlikleri, kriz tekrar etmez, rüzgâr hedefi yalnız rüzgâr karosuyla; dönüş kodu gidiş-dönüş testleri). Play Mode'da 5 sondalı run: son ayarla boşta run Başarısız 12 puan, vadi çökmeden; açılım ağacı ekranı ve kart yenileme görüntüyle kontrol edildi. Kayıtlar test öncesi yedeklenip geri yüklendi | Yapıldı |
