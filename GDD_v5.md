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
10. [Çevrenin görünüşü](#gorunus)
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
- **Yol temizliği (Uygulandı):** karolar kurulduktan sonra sahnede kalan eski sabit harita eşyaları (pazar tezgâhı, su varili, saha istasyonunun ağaç ve kayaları) ve karo tabelaları yol yüzeyine basıyorsa en yakın boş, kuru zemine taşınır (çatı, su ve başka yapı kabul edilmez). İlk prototipin "PowerStarter" demo köşesi (filtresiz fabrika, iki mini bölge, balık göleti, çalı yangını, üç ağaç) her vadide aynı yerde duruyordu, fabrikası yola basıyordu; artık kapatılıyor (yangın, şablon olarak sahnede pasif kalır).
- **Çöp ve trafik (Uygulandı):** run başında her bölgeye kirliliğiyle orantılı teneke ve şişe saçılır (hava, su ve toprak kirliliği ortalamasının %5 üstündeki her 5 puana 1 parça, bölge başına en çok 14). Temiz bir Buzul'da hiç olmaz, %55 kirli bir fabrika bölgesinde 10 parça olur. Her parça süpürgeyle 2 metal ya da 2 plastik verir. Yollar tek bir bağlı ağ olduğu için vadide 7 yaya ve 4 araba dolaşır (`EcoTraffic`): yol hücreleri 1,5 m'lik ızgarayla örneklenir, A* arabaları yolun ortasından, yayaları kaldırımdan götürür. Her biri 1–3 dakikada bir yere çöp atar (yaya ayağının dibine, araba camdan yol kenarına). Bu, oyuncuya küçük ama sürekli bir metal ve plastik kaynağıdır; vadide en çok 40 parça çöp birikir. Arabalar oyuncunun ve öndeki arabanın önünde durur. Hangi sebeple olursa olsun 4 sn'den uzun duran yaya ya da araba başka bir yol planlar (önü kesildiyse geri döner). Geniş dönen arabanın geride kalmış ara noktası, rota üzerinde geçildiği anda sayılır; yaya ve arabaların katı collider'ı vardır, içlerinden geçilmez. Çöp `litter` bayrağı taşır, "atık yığınlarını temizle" hedefine sayılmaz, böylece trafik o hedefi bozamaz. Erkek yaya oyuncuyla aynı modeli paylaştığı için açık bir kıyafet tonuyla ayrılır. Yayalar oyuncuyla aynı boydadır (oyuncu ölçeği 1,4, kişiden kişiye ±%6), adım animasyonu yürüme hızına göre hızlanır, kayma olmaz. Modeller `EcoQuest/Build Traffic Assets` ile üretilir (`Assets/Resources/EcoTraffic/`).
- **Şehir yerleşimi (Uygulandı):** rastgele kurulan vadide bir yapı yola taşıyorsa, run başında yapı 0,5 m'lik adımlarla en yakın boş yere (en çok 8 m) kaydırılır. Önce yalnız boş zemin denenir, olmazsa kaldırım kabul edilir; asfalt, su ve başka yapı hiçbir zaman kabul edilmez. Önemli yapı (Ev, market, kütüphane, istasyonlar) yer bulamazsa yolunu kesen sıradan bir "House" ya da "Building" kaldırılır. Şehirdeki çıkmaz sokakların yuvarlak dönüş cepleri (3,8 m yarıçap) boş asfalt disk gibi duruyordu; artık her birinin ortasında bordürlü, çimli, ağaçlı bir ada vardır (1,4 m). Çevresindeki 2,4 m'lik halka araba dönüşüne yeter; ada katıdır, trafik çevresinden dolaşır.

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
| **tree_planting** (yeni) | ağaç | Güneş (her vadide) | Oyuncunun diktiği, sağlıklı ve en az yarı büyümüş fidan ya da ondan büyüyen ağaç ≥ | 6 / 10 / 14 |
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

**Açılım ağacı (12 düğüm, toplam 560 kredi).** Derin bir düğüm, aynı daldaki "Önce" düğümlerinden **birini** ister. Brifingde ve değerlendirme ekranında "Açılım ağacı" düğmesiyle açılır.

| Dal | Düğüm | Kredi | Önce | Etki |
| --- | --- | --- | --- | --- |
| Başlangıç | Başlangıç hibesi | 30 | — | Her run +20 metal, +20 plastik |
| Başlangıç | Tohum bankası | 35 | — | Her run +10 ağaç tohumu |
| Başlangıç | Su planları | 45 | Başlangıç hibesi | Pompa, arıtma, depo tarifleri baştan açık |
| Başlangıç | Verimli aletler | 50 | Hibe ya da tohum bankası | Alet elektriği %15 az |
| Uzmanlık | Su dalı | 40 | — | [K] uzmanlıkta Su dalı |
| Uzmanlık | Ekoloji dalı | 40 | — | [K] uzmanlıkta Ekoloji dalı |
| Uzmanlık | Enerji planları | 45 | — | Rüzgâr türbini ve doğal akış santrali run başından kurulabilir ("Mahalleye ilk enerji" ve "Gölü canlandır" projelerini beklemez) |
| Uzmanlık | Usta | 80 | Su ya da Ekoloji dalı | Dallar 3. kademeye çıkar (yoksa en çok 2) |
| Sabah ve kriz | Erken uyarı | 40 | — | Akşam krizi öğleden haber verilir |
| Sabah ve kriz | Kart yenileme | 45 | — | Günde bir kez sabah kartlarını yenile |
| Sabah ve kriz | Geniş seçenek | 60 | Yenileme ya da erken uyarı | Sabah 4 kart |
| Zorluk | Kriz zorluğu | 50 | — | Kriz zorluğu seçilebilir, 1,4× kredi |

**Uzmanlık bağlantısı (v4 S4'ün çözümü):** Enerji dalı herkese açıktır; Su ve Ekoloji dalları ve 3. kademe meta açılımıdır. Böylece bir run'da her şey alınamaz, her run'ın uzmanlık seçimi anlam kazanır. YEP ve hak kazanımı (her 40 YEP bir hak) v4 §7 ile aynıdır.

**Tempo hesabı:** başarılı bir Normal run ortalama 35–45 kredi getirir; ağacın tamamı ≈ 13–15 run'da (≈ 14–17 saat) dolar. İlk iki run'da ilk açılım alınır.

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
| V3 | Kredi temposu (ağacın tamamı ≈ 14 run) uzun mu? | runs.csv ile ölçülecek |
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
| `World/EcoEnvironmentMood.cs` | Bölge durumunu ekranda gösterir (duman, renk, gökyüzü, polen) |

<a id="gorunus"></a>
## 10. Çevrenin görünüşü

**Karar:** göstergeler panelde kalmaz, oyuncunun durduğu bölgede ekranda görülür. Her panel satırının kendi işareti var; bölge sınırı geçilince 3–4 saniyede yumuşakça değişir (`EcoEnvironmentMood`, yalnız görsel).

| Gösterge | Ekranda | Eşik (Denge taslağı) |
| --- | --- | --- |
| Hava kalitesi (AQI) | Duman: sis oyuncunun çevresine kapanır, sarı-kahveye döner, havada toz uçuşur, güneş zayıflar. İlk girişte "hava kirli: duman görüşü kapatıyor" bildirimi | AQI 50'ye kadar yok, 200'de tam |
| Karbon yükü (vadi) | Gökyüzü ve ufuk ılık bir pusa, güneş ışığı sarıya kayar | 12'ye kadar yok, 40'ta tam |
| Ekosistem sağlığı | Renk: hasta bölge grileşir, kontrast ve ışık düşer, kenarlar kararır | 70 ve üstü tam renk, 25'te gri |
| Biyoçeşitlilik | Gündüz havada polen ve tohumlar uçuşur (kuş ve kelebekler `EcoHabitat`'ten) | 45'te başlar, 90'da tam |
| Su kalitesi | Göl rengi berraktan bulanık yeşile (`EcoLakeTint`, v4) | — |
| Toprak kalitesi | Zemin yeşilden tozlu kahveye (`EcoPollutionField`, v4) | Toprak kalitesi 50'de hafif solukluk, kötüleştikçe kahve |

İzometrik kamera oyuncuya ≈70 m uzaktan baktığı için duman bandı oyuncunun çevresine yerleştirilir: tam dumanda oyuncu okunur kalır (≈%25 sis), ekranın uzak kenarı kaybolur. Renk ayarı için kamerada son işleme açılır; sahnede başka hacim yoktur.

<a id="gunluk"></a>
## 21. Değişiklik günlüğü

| Tarih | Madde | Durum |
| --- | --- | --- |
| 26 Kasım | **Rastgele vadi run'ları, Aşama 3 (meta, içerik, denge).** **Karo dönüşleri:** yan karolar tohumla 0/90/180/270° döner; vadi kodu dönüşü rakamla taşır (`Grove1-DirtyLake-Industry-Wind3`, rakamsız eski kodlar 0° sayılır), minimap karoları da döndürür, dışa bakan 6 yol ağzı haritada zeminle örtülür. **Açılım ağacı (§5):** 4 grup (Başlangıç, Uzmanlık, Sabah ve kriz, Zorluk), 11 açılım, toplam 515 kredi, iki seviyeli ön koşullar; yeni açılımlar: Planlar (su tarifi baştan açık), Verimli aletler (enerji kullanımı ×0,85), Su/Ekoloji dalı, Uzman (uzmanlık 3. kademe), Kart yenileme, Erken uyarı, 4. kart, Kriz zorluğu. Uzmanlık dalları ve 3. kademe artık meta açılımına bağlı (Enerji hep açık). Sonuç ekranından ve brifingden açılan 4 sütunlu ağaç ekranı. **İçerik:** 6 yeni sabah kartı (toplam 19: fidanlık, dere temizliği, yeşil çatılar, karbon vergisi, gece devriyesi, hurda mezadı), 3 yeni kriz (toplam 10: sel, sıcaklık terslemesi, istilacı tür; aynı kriz üst üste gelmez), 2 yeni hedef (rüzgâr çiftliği yalnız Rüzgâr Tepeleri olan vadide, ağaçlandırma: büyüyen dikili ağaç sayısı); boşta tutan `city_air` çıkarıldı. Günde bir kez kart yenileme. **Denge (§6):** zarlanan vadide doğal toparlanma ×0,35, yabani ağaçların emmesi ×0,12 ve örtü büyümesi başlangıç örtüsüyle sınırlı; ölçülen hedef eşikleri vadinin başlangıç değerine göre sıkılaştırılıyor; brifing bunu söylüyor. `runs.csv` run başına bir satır; editörde `EcoBalanceProbe` boşta oyuncu sondası. **Doğrulama:** EditMode 18/18 (yeni `EcoMetaTests`: ağaç yapısı, kart/kriz kimlikleri, kriz tekrar etmez, rüzgâr hedefi yalnız rüzgâr karosuyla; dönüş kodu gidiş-dönüş testleri). Play Mode'da 5 sondalı run: son ayarla boşta run Başarısız 12 puan, vadi çökmeden; açılım ağacı ekranı ve kart yenileme görüntüyle kontrol edildi. Kayıtlar test öncesi yedeklenip geri yüklendi | Yapıldı |
| 26 Kasım | **Aşama 3 eksik taraması ve düzeltmeler.** (1) **Ağaçlandırma hedefi** yalnız fidanları sayıyordu; olgunlaşan fidan ağaca dönüşüp `PlantedSeed`'i bıraktığı için sayaç oyuncu ilerledikçe düşüyordu. Artık oyuncunun diktiği büyümüş ağaçlar da sayılıyor (`EcoRun.PlantedTrees`, `EcoVegetation.planted`). (2) **Plandaki bina açılımı eksikti:** rüzgâr türbini "Mahalleye ilk enerji", hidro "Gölü canlandır" projesine bağlıydı. Yeni düğüm **Enerji planları** (Uzmanlık, 45 kredi) ikisini run başından açıyor; ağaç 12 düğüm / 560 kredi. (3) **Yabani ağaç örtü tavanı** kayda yazılmıyordu (yüklemede o anki örtü sayılıyordu); başlangıç örtüsü bölge anahtarıyla run durumuna yazılıyor, eski kayıtlarda eski davranış sürüyor. Taranıp sorun bulunmayanlar: karo bölgelerinin anahtarları keşif ve tarif aramalarıyla uyumlu (Koruluk, Doğu Gölü…), bir vadide aynı karo iki kez çıkmıyor, büyümüş ağaçlar kayıttan dikili olarak dönüyor, sıkılaştırılmış eşikler kayda yazılıyor. Bilinçli olarak açık kalanlar: karo başına ikinci tasarım (V1), run başı yükleme ekranı, gerçek oyuncuyla denge. **Doğrulama:** EditMode 19/19 (yeni: Enerji planları rüzgâr ve hidroyu açar). Play Mode'da tohum 198: büyütülen dikili ağaç sayaçta 1, 7 bölgenin başlangıç örtüsü JSON gidiş-dönüşünde korunuyor, açılım ağacı ekranında 4. Uzmanlık kartı sığıyor. Kayıtlar değişmedi | Yapıldı |
| 26 Kasım | **Çevre görünüşü ve vadi yerleşimi.** (1) **Göstergeler ekranda (§10):** kirli havada duman ve toz, yüksek karbonda ılık pus ve sarı güneş, hasta bölgede grileşen renk, sağlıklı bölgede polen; su ve toprak v4'teki göl ve zemin renkleriyle. (2) **Yolda duran yapılar (§3):** pazar tezgâhı ve su varili şehir göbeğindeki kavşağın, saha istasyonunun ağaç ve kayaları yolun üstündeydi; kurulumdan sonra en yakın boş zemine taşınıyor. Aynı tarama karo tabelalarını da düzeltiyor. (3) **Her vadide aynı yerde duran köşe:** ilk prototipin PowerStarter demo seti (filtresiz fabrika yolda, mini bölgeler, gölet, yangın) kapatıldı; ayrıca karo bölgeleriyle çakışan iki fazla bölge ölçümden çıktı. **Doğrulama:** EditMode 20/20 (yeni: görünüş eşikleri panelle tutarlı). Play Mode'da tohum 777: yolda duran nesne 30'dan 2'ye indi (kalanlar bir bölge etiketi ve Fabrika Gölü'nün atık su çıkışı, ikisi de karonun kendi tasarımı); Güneş Ovası temiz (AQI 41) ve kirli (AQI 191, sağlık düşük, karbon yüksek) hâlde ekran görüntüsüyle karşılaştırıldı. Kayıtlar geri yüklendi | Yapıldı |
| 26 Kasım | **Görünüş karşılaştırması (normal / az).** Her gösterge için yalnız o değer değiştirilerek aynı yerden normal ve az hâl ekran görüntüsü alındı (Temp/shots/mood). Bulunan ve düzeltilenler: (1) toprak kalitesi 50'de zemin hiç değişmiyordu; zemin tonu artık orta kirlilikte başlıyor (temiz zemin yine yeşil). (2) polen ve duman tanecikleri izometrik kameradan seçilemeyecek kadar küçüktü; büyütüldü. (3) Fabrika Gölü'ndeki kirletici giriş tabelası karo prefabında malzemesiz kaldığı için mor görünüyordu; malzeme açılışta yeniden kuruluyor. Az hâller bilinçli olarak hafif: hava ve toprak için fark küçük ama görülür, su ve sağlık belirgin. EditMode 20/20. Kayıtlar geri yüklendi | Yapıldı |
| 26 Kasım | **Demo köşesi editörde de kapalı.** Oyunda zaten kapatılan PowerStarter demo seti (bacalı fabrika, kirli zemin dairesi, balık göleti, çalı yangını, üç ağaç) `Assets/Resources/EcoInfrastructure/PowerStarter.prefab` içinde pasif yapıldı; sahne görünümünde artık çıkmıyor. Çalı yangını pasif hâlde kalıyor çünkü run'lar ve kayıtlar onu yangın şablonu olarak kopyalıyor. Play'de doğrulandı: demo görünmüyor, `SpawnFires` 2 yangın çıkardı | Yapıldı |
| 27 Kasım | **Çöp, trafik ve Kaynaklar paneli.** (1) **Başlangıç çöpü (§3):** her bölgeye kirliliğiyle orantılı teneke ve şişe saçılır (tohum 198: Fabrikalar 10, Fabrika Gölü 9, Şehir 4, temiz bölgeler 0–2, toplam 27). (2) **Yaya ve araçlar (§3):** 7 yaya ve 4 araba bağlı yol ağında dolaşır ve zaman zaman çöp atar; çöp süpürgeyle metal ve plastik verir ama atık yığını hedefine sayılmaz. (3) **Kaynaklar paneli açılmıyordu:** HUD "Sade" kipindeyken Kaynaklar ve bölge kartları zorla kapalı çiziliyordu, aç düğmesi bir şey yapmıyordu. Artık Sade kipte de açılıp kapanıyorlar. **Doğrulama:** EditMode 21/21 (yeni: başlangıç çöpü kirlilikle artar). Play'de tohum 198: trafik 1077 şerit ve 816 kaldırım hücresi buldu; çöp atma 16'dan 27'ye çıkardı, atık yığını hedefi 7'de kaldı; oyuncunun önünde bekleyen araba 4 sn sonra geri dönüp uzaklaştı; Sade kipte kart açıldı ve kapandı. Kayıtlar geri yüklendi | Yapıldı |
| 27 Kasım | **Trafik takılması ve içinden geçme.** Gerçek oyunda run duraklatılmış sabah kartının arkasında başlıyor. Karolar yeni yerleşmişken fizik adımı çalışmadığından collider'lar eski yerlerindeydi; yol taraması başlangıç kavşağı çevresinde birkaç metre yol buldu, bütün yaya ve arabalar oraya yığılıp kaldı. Artık tarama kart kapandıktan sonra ve `Physics.SyncTransforms` ile yapılıyor; 300'den az şerit hücresi bulursa 2 sn sonra yeniden deniyor. Başlangıç çöpü, yangın ve atıklar da yer aramadan önce aynı senkronu yapıyor. Yaya ve arabalara katı (kinematik) collider eklendi, içlerinden geçilmiyor. Yayalar da oyuncunun önünde duruyor, çöp araba tavanına düşmüyor. **Doğrulama:** EditMode 21/21. Play'de tohum 4242, kart 6 sn açık bekletildi: 1019 şerit ve 795 kaldırım hücresi bulundu, 11 nesne haritaya dağıldı ve hareket etti; oyuncu arabaya doğru itildiğinde 0,86 m'de durdu. Kayıtlar geri yüklendi | Yapıldı |
| 27 Kasım | **Yaya boyu, yola taşan yapılar ve dönüş cepleri.** (1) Yayalar oyuncudan küçüktü; artık oyuncu ölçeğinde (1,4) ve adımları hıza bağlı (§3). (2) Rastgele vadide yola taşan yapılar run başında en yakın boş yere kaydırılıyor; yer bulamayan önemli yapı için yolu kesen sıradan bir ev kaldırılıyor (§3 Şehir yerleşimi). Ölçümde asfalt üstünde yapı kalmadı; yalnız birkaç çatı saçağı kaldırım üstüne uzanıyor. (3) Şehirdeki 4 yuvarlak dönüş cebine bordürlü, ağaçlı ve çimli ada kondu. Deneme sırasında düz çim yamasının yüksekliğe göre ölçeklenip 144 m'lik bir plakaya dönüştüğü ve tüm zemini turkuaza boyadığı görüldü; süs ölçeklemesi en büyük kenara göre yapılacak şekilde düzeltildi. EditMode testleri 21/21. |
| 27 Kasım | **Trafik takılması ve kare hızı.** (1) Başlangıç kavşağında bir araba, geniş dönüşte yakına gelemediği bir ara noktada takılıyordu: sıradaki hedefi kendi altındaydı, ne ilerliyor ne dönüyordu, arkasındaki iki araba da onu bekliyordu. Artık rotada geçilen ara nokta sayılıyor; 4 sn'den uzun duran herkes yeni yol planlıyor (§3). Ölçümde 3,5 dk sonra 7 yaya ve 4 arabanın hepsi hareket ediyordu. (2) **Kare hızı (en az 60 FPS):** akşam aydınlatması 10 sn'de bir sahnedeki ~3000 renderer'ın hepsini baştan inceliyordu (15 ms takılma), artık her renderer bir kez inceleniyor. Kirlilik haritası her bölgeyi doku pikseli başına değil, yenileme başına bir kez örnekliyor (4 ms → <1 ms). Ekran dışındaki göl dalgaları güncellenmiyor. Ağaçlar bölgesini 2 sn'de bir, yaban hayatı sağlıklı ağaç sayısını 0,5 sn'de bir buluyor (önceden her kare, her ağaç için). Kayıt kontrolü oyuncu aletini her kare aramıyor. HUD panelleri ayrı alt tuvallerde; bir sayı değişince bütün HUD yeniden dizilmiyor. Editörde (tohum 198, şehir, yağmurlu): median kare 8 ms (~125 FPS), 16,7 ms'yi aşan kare %0,3, en kötü %1'lik dilim ~70–80 FPS. EditMode testleri 21/21. |
| 27 Kasım | **Donmuş yaya ve arabalar.** Trafik nesneleri `HideFlags.DontSave` ile kuruluyordu; Unity bu nesneleri sahne yeniden yüklenince silmiyor. Her run başlangıcında ve her "Devam et"te eski yayalar ve arabalar sürücüsüz kalıp yerinde donuyor, üstüne yeni bir takım ekleniyordu (editörde Play'den çıkınca da kalıyorlardı). Ölçümde sahnede 10 eski takım, yani 70 donmuş yaya ve 40 donmuş araba vardı; bunlar collider ve animatörleriyle kare hızını da düşürüyordu. Trafik artık sıradan bir sahne nesnesi: iki ardışık run yüklemesinden sonra sahnede tek takım (7 yaya, 4 araba) kaldı, 20 sn'de yayalar 10–14 m, arabalar 41–50 m yol aldı; Play'den çıkınca kalıntı kalmadı. EditMode testleri 21/21. |
| 28 Kasım | **Geri bildirim 1: çöp atma görünür oldu.** Yaya çöp atarken yavaşlıyor, sağ kolunu omzunun arkasına kaldırıp ileri savuruyor. Teneke ya da şişe elinden çıkıp dönerek 2–3 m öne uçuyor, arkasında ince beyaz iz kalıyor. Araba çöpü camdan yol kenarına fırlatıyor. Çöp düştüğü yerde toz bulutuyla bir kez sekiyor, sonra sarı bir halka ve kıvılcımlarla toplanabilir hale geliyor. 20 sn boyunca hafifçe atıyor ve 2,5 sn'de bir küçük halka veriyor. Süpürge çöpü tam görüldüğü yerden alıyor. Havadaki kopya yalnızca görüntü: collider'ı yok, kaydedilmiyor (§3). **Doğrulama:** Play'de tohum 198 ile yavaş çekimde kolun ileri uzandığı, şişenin havada izle uçtuğu ve sarı halkayla yere düştüğü görüldü. 4 araba atışının dördü de yere indi (çöp 30 → 34, havada kalan yok). Play'den çıkınca kalıntı kalmadı. Kayıtlar geri yüklendi | Yapıldı |
| 28 Kasım | **Geri bildirim 2: toplama yazısı, süpürgeye çekilme, suçüstü yaya.** (1) Süpürge ya da geri dönüşüm cihazı bir atığı alınca kazanç yerden yukarı süzülen bir yazıyla gösteriliyor: metal için "+2 metal", plastik için "+2 plastik", organik için "+3 organik". Her tür kendi renginde ve ayrı satırda; hepsine "metal" yazılmıyor. (2) Alınan atığın görüntü kopyası dönerek ve büzülerek ağızlığa çekiliyor, ortadan birden kaybolmuyor. (3) Oyuncu 14 m içindeyken çöp atan yaya başını oyuncuya çevirip ona bakıyor, başının üstünde sarı bir "!" beliriyor. Sonra utanıp 4 sn boyunca normal hızının 1,9 katıyla uzaklaşıyor (§3). **Doğrulama:** Play'de tohum 198: teneke toplanınca "+2 metal" yazısı çıktı ve kutu ağızlığa çekildi. Yayanın başında "!" göründü; kaçarken animasyon hızı 1,56'ya çıktı (normal yürüyüşün 1,9 katı). Play'den çıkınca kalıntı kalmadı. Kayıtlar geri yüklendi. Yazı boyutu sonradan büyütüldü, yeni boyut oyunda görülmedi | Yapıldı |
| 28 Kasım | **Geri bildirim 3: suçüstü araba.** Oyuncu 14 m içindeyken camdan çöp atan arabanın tavanında sarı bir "!" beliriyor. Sürücü gaza basıyor: arkadan bir egzoz bulutu çıkıyor, araba 3,5 sn boyunca normal hızının 1,5 katıyla gidiyor ve arkasında duman izi bırakıyor. Kaçarken de öndeki araba ve oyuncu için duruyor (§3). **Doğrulama:** Play'de tohum 198: "!" arabanın üstünde göründü, kaçış süresi başladı, egzoz izi yakın çekimde görüldü (ilk denemede soluktu, büyütülüp koyulaştırıldı). Şişe yol kenarına sarı kıvılcımla düştü. Kayıtlar geri yüklendi | Yapıldı |
| 28 Kasım | **Oyuncu hızı animasyona uyduruldu.** Yürüme ve koşma klipleri yerinde oynuyor; ölçümde karakter ölçeğinde (1,4) yürüme klibi 0,8 m/s'ye, koşma klibi 3,0 m/s'ye uyuyor. Oyuncu ise 5,3 m/s yürüyüp 8,3 m/s koşuyordu, ayaklar yerde kayıyordu. Artık normal hareket 3,5 m/s, Shift ile 5 m/s. Hıza göre önce yürüme, sonra koşma klibine geçiliyor; koşma klibinin hızını aşan kısımda klip kendisi hızlanıyor (normalde 1,16 kat, Shift ile 1,66 kat). Düşmanların kovalama hızı (2,6 m/s) oyuncunun altında kalıyor. **Doğrulama:** Play'de oyuncu 3,5 m/s gitti, klip 1,16 kat oynadı. Yere basan ayak en fazla 0,2–0,4 m/s kaydı (önceden 5 m/s'nin üstündeydi). Shift oyunda ayrıca denenmedi. Kayıtlar geri yüklendi | Yapıldı |
| 28 Kasım | **Geri bildirim 4: inşa ve bağlantı; inşa modunda yürüme.** (1) İnşa modunda artık WASD ile yürünüyor: oyuncu yapıyı koymak istediği yere yürüyebiliyor, menüyü kapatıp açması gerekmiyor. Sağ tık yine iptal ediyor, yürüme emri vermiyor. Bu yüzden inşa modu dünyayı artık 0,1 hıza yavaşlatmıyor; paneller yavaşlatmayı sürdürüyor. (2) Önizleme hücreden hücreye kayarak geçiyor; geçerliyken hafifçe nabız atıyor. Geçersiz yere tıklanınca yanıp sönüyor ve yana titriyor. (3) Kurulan yapı yerden basılıp esneyerek yükseliyor (0,2 sn büyüme, 0,65 sn sönen salınım). Tabanından toz halkası ve kıvılcım çıkıyor, harcanan malzeme kırmızı "−12 metal / −6 plastik" olarak süzülüyor. (4) Kablo ya da boru ilk uçtan ikinci uca 0,45 sn'de çizilerek uzanıyor, uçta kıvılcım gidiyor. Ulaşınca iki uçta kıvılcım çıkıyor, hat boyunca bir ışık atımı koşuyor (elektrik sarı, temiz su mavi, kirli su kahve). İlk uç seçilince de orada kıvılcım çıkıyor. Kazanç ve harcama yazılarının satır arası açıldı. **Doğrulama:** Play'de tohum 198: geri dönüşüm tesisi basık başlayıp yükseldi, maliyet yazısı okunaklı. Kablo yarıya kadar çizilmiş halde görüldü. İnşa modunda gerçek klavyeyle W basılı tutulunca oyuncu yürüdü, timeScale 1 kaldı. Kayıtlar geri yüklendi | Yapıldı |
| 28 Kasım | **Geri bildirim 5: kaynak sayaçları.** Kaynaklar panelinde bir sayı değişince ikonu ve sayısı 0,35 sn büyüyüp küçülüyor, sayaç zemini ve sayı yeşil (artış) ya da kırmızı (azalış) parlıyor. Sağ köşeden "+5" / "−12" hayaleti süzülüp 1,1 sn'de soluyor; art arda gelen aynı yönlü değişimler tek hayalette toplanıyor ("+2", sonra "+4"). Malzeme yetmeyen bir harcamada (inşa, kablo, boru, pazar; `TrySpendMaterials`) ve inşa modunda malzemesi yetmeyen yere tıklanınca eksik sayaç kırmızı yanıp titriyor. Oyuncu neyin eksik olduğunu yazıyı okumadan görüyor. Panel daraltılmışken eski "+N" bildirimi aynen kalıyor. **Doğrulama:** Play'de tohum 198: +5 plastik yeşil, −5 metal kırmızı, yetmeyen gübre kırmızı titredi. 1,6 sn sonra dört sayaç da yerleşim konumuna ve ölçeğine döndü (ilk denemede etiketlerin 2× raster ölçeği unutulmuştu, düzeltildi). Kayıtlar geri yüklendi | Yapıldı |
| 28 Kasım | **Geri bildirim 6: hedef, YEP ve uzmanlık hakkı.** (1) YEP kazanılınca oyuncunun üstünden altın "+20 YEP" süzülüyor ve altın kıvılcımlar çıkıyor. Aynı anda gelen ödüller üst üste diziliyor. (2) 40 YEP sınırı geçilince oyuncunun çevresinde üç kat altın halka yayılıyor ve ekranın üstünde "YENİ UZMANLIK HAKKI · Hak N / 9 · [K] uzmanlık" bandı açılıyor. (3) Run hedefi tamamlanınca oyuncudan renkli konfeti saçılıyor ve "HEDEF TAMAMLANDI" (bonusta "BONUS HEDEF") bandı hedef adıyla birlikte açılıyor. Run kartında o hedefin satırı 3 sn yeşil nabızla parlıyor, tik işareti büyüyerek geliyor. Aynı metni tekrarlayan eski toast satırı kaldırıldı. Bant 2,6 sn sürüyor: taşarak açılıyor, bekliyor, solup yukarı kayıyor. Ödüller `EcoProgression.RewardGranted`'dan okunuyor; kayıt yüklenince tetiklenmediği için yüklemede kutlama olmuyor. **Doğrulama:** Play'de tohum 198: iki saha başarısı (+20 +20) hak bandını, altın kıvılcımı ve yazıyı gösterdi. `world_carbon` hedefi tamamlanınca bant, konfeti, satır parıltısı ve tik görüldü. Kayıtlar geri yüklendi | Yapıldı |
| 28 Kasım | **Geri bildirim 7: düşman vuruşu.** (1) Vurulan yaratık bir anlığına beyaza parlıyor (alt-mesh başına MaterialPropertyBlock; ~0,1 sn'de sönüyor). Mevcut sarsılma pozu ve metal atışının geri itmesi korundu. (2) Hasar sayısı: akış her karede hasar verdiği için hasar toplanıyor ve saniyede en çok ~3,5 kez başın üstünde turuncu "−12" olarak süzülüyor. Her sayıda yaratığın renginde küçük bir kıvılcım halkası çıkıyor (Alev turuncu, Salya yeşil, Teneke gümüş, Asit sarı-yeşil, Duman gri). (3) Yenilince yerde renkli bir halka ve gövdeden yukarı saçılan parçacıklar çıkıyor. (4) Ganimet artık ayağın dibinde belirmiyor: gövdeden fırlayıp dönerek 0,9–1,4 m öteye kavisle uçuyor, bir kez sekiyor, sarı kıvılcımla oturuyor ve sonra eskisi gibi sallanıyor. Uçarken de toplanabilir. **Doğrulama:** Play'de tohum 198, Salya: "−12" yazısı göründü, gövde rengi (0,19; 0,40; 0,28)'den (0,65; 0,75; 0,69)'a açıldı ve görüntüde beyazlaştı. Yenilince halka ve parçacıklar çıktı, plastik ganimet havada görüldü ve 1 m öteye indi. Kayıtlar geri yüklendi | Yapıldı |
| 28 Kasım | **Geri bildirim 8: dünyadaki iş yerleri.** (1) **Balık tutma:** olta atılınca kırmızı-beyaz bir şamandıra kavisle suya uçuyor ve sıçrama halkası yapıyor, sonra sallanıyor. Balık 2,3. saniyede vuruyor: şamandıra batıp çıkıyor, üstünde sarı "!" beliriyor. Yakalayınca su sıçrıyor, balık dönerek oyuncuya uçuyor, "+1 balık" (kirli suda "+1 kirli balık") yazısı ve kıvılcım çıkıyor. İptalde ya da yakalamada şamandıra geri sarılıyor. Şamandıra, su hacmi açık sudan büyük olduğu için kıyıdan içeri yürünerek bulunan ilk gerçek su noktasının 1 m ötesine düşüyor. (2) **Fabrika filtresi:** filtre çerçeveye taşarak "oturuyor", altın kıvılcım ve "Filtre takıldı · hava −%80" yazısı çıkıyor, bacadan beyaz bir puf yükseliyor. Havadaki koyu duman artık silinmiyor, kendiliğinden dağılıyor; böylece duman griden beyaza dönüyor (yüklemede eskisi gibi hemen temizleniyor). **Temiz enerji dönüşümü:** yeşil yer halkası, kıvılcım ve "TEMİZ ENERJİ" yazısı. (3) **Atölye:** paket hazır olunca ve acil şarj bitince oyuncunun üstünde altın "+ Küçük hava filtresi" / "+15 EB" yazısı ve kıvılcım çıkıyor. (4) **Pazar:** başarılı alım, satım, geliştirme ve destekte kart yeşil parlayıp hafifçe büyüyor, üstünden koyu yeşil "Alındı!" / "Satıldı!" / "Geliştirildi!" / "Tamam!" yükseliyor. Olmayan işlemde kart kırmızı titriyor ve "Olmadı" yazıyor. **Doğrulama:** Play'de balık tutuldu (şamandıra, sıçrama, uçan balık, "+1 balık"); şamandıra önce kumsala düştü, düzeltildi ve suya indi. Fabrikaya filtre takıldı, yazı ve beyaz puf görüldü. Pazarda demir top alındı, balık satıldı, iki kart yeşil parladı; yazı soluk ve komşu kartın altında kaldığı için koyulaştırıldı ve kart öne alındı. Keşif tarama halkası bu adıma girmedi. Kayıtlar geri yüklendi | Yapıldı |
| 28 Kasım | **Geri bildirim 9: run akışı.** (1) **Sabah kartı:** seçilen kart küçülerek kavisle sağdaki run çipine uçuyor (0,7 sn). Kart oraya varınca çip altın renkte parlıyor, oyuncunun çevresinde altın halka ve kıvılcım çıkıyor, başının üstünde kartın adı yükseliyor ("Bilim ekibi"). Böylece kartın etkisi vadiye 'iniyor'. (2) **Akşam krizi:** kriz kartı açılınca ekranın kenarları krizin renginde nabız gibi atıyor: sıcak hava dalgası ve yangında turuncu, petrol sızıntısında mor, sel ve istilacı türde mavi. Kenarlar 8 saniyede sönüyor; kart kapandıktan sonra HUD okunabilsin diye yarı yarıya hafifliyor. Gece yarısına kadar run çipinde krizin renginde yanıp sönen bir "Kriz: Sıcak hava dalgası" satırı kalıyor. **Doğrulama:** Play'de "Bilim ekibi" kartı seçildi; kart uçarken, çipe varırken ve inişte halka ve yazıyla görüntülendi. Saat 18:30'a alınıp akşam tetiklendi ve sıcak hava dalgası çıktı. Turuncu kenar ve çipteki kriz satırı görüldü; kart kapandıktan sonra kenar HUD'u fazla kapattığı için hafifletildi. Kayıtlar geri yüklendi | Yapıldı |
| 28 Kasım | **Geri bildirim 10: oyuncu hareketi.** (1) **Koşu tozu:** koşarken (yürüme hızının üstünde) her adımda sırayla sol ve sağ ayaktan yumuşak bir toprak bulutu kalkıyor. Koşuya başlarken topukların arkasından bir toz savruluyor, koşudan aniden durunca ayakların önünde bir kayma pufu çıkıyor. Bir yükseklikten düşüp yere inince ayakların çevresinde bir toz halkası açılıyor. Yürürken toz yok, ekran sakin kalıyor. Toz kendi parçacık sistemini kullanıyor: zamanla büyüyüp yumuşakça soluyor, çünkü ortak efekt kıvılcımları uzaktaki izometrik kamerada nokta gibi kalıyordu. (2) **Alet değiştirme:** yeni alet ele küçükten taşarak oturuyor, elde beyaz bir halka ve birkaç sarı kıvılcım çıkıyor. Aleti bırakınca (eller) küçük gri bir puf çıkıyor. Araç çubuğunda seçilen yuvanın simgesi merkezinden büyüyüp hafifçe zıplıyor. Kayıt yüklenirken ve inşa modu ya da pazar aleti yeniden seçtiğinde efekt oynamıyor. **Doğrulama:** Play'de alet değişince eldeki pop ve kıvılcım görüldü. Gerçek klavyeyle Shift+D koşusu yapıldı; toz önce nokta gibi kaldı, iki kez büyütüldü ve rengi koyulaştırıldı, son hâlde arkada toprak bulutları seçiliyor. Düşüş halkası ve kayma pufu ayrıca görüntülenmedi. Kayıtlar geri yüklendi | Yapıldı |
| 28 Kasım | **Geri bildirim 11: keşif taraması.** (1) **Tarama halkası:** tarayıcı her taradığında oyuncunun çevresinde camgöbeği bir halka zemine oturarak 7 m'ye kadar genişliyor ve sönüyor. Halka eğimlere ve çukurlara uyuyor. Düğme basılı tutulunca halka en fazla saniyede bir çıkıyor, ekran dolmuyor. (2) **Hedef:** bir şey hedeflenmişse tarayıcıdan ona kısa bir ışın uzanıyor, hedefin tabanında boyuna göre bir işaret halkası nabız gibi atıyor ve küçük bir kıvılcım çıkıyor. (3) **Yeni keşif:** tarama yeni bir bölgeyi ya da tarifi açtıysa iki geniş halka daha yayılıyor, kıvılcım yükseliyor ve oyuncunun üstünde "YENİ KEŞİF · Güneş Ovası" yazıyor. **Doğrulama:** Play'de tarayıcı seçildi ve tarama yapıldı; halka zeminde görüldü. Bölge bilgisi silinip yeniden tarandı; çift halka ve "YENİ KEŞİF · Güneş Ovası" görüldü. Bir ağaca ışın gönderildi, ışın görüldü; ağaç uzakta kaldığı için hedef halkası kadraja girmedi. Gerçek fareyle nişan alınarak denenmedi. Kayıtlar geri yüklendi | Yapıldı |
| 28 Kasım | **Geri bildirim 12: bakılan nesnenin bilgisi dünyada.** Oyuncu ekran kaydında alttaki bilgi panelinin sürekli değiştiğini gösterdi (Şarj istasyonu · Depo 131→138/240, her karede yeni sayı). Bu bilgi artık panelde değil, nesnenin kendi üstünde, kameraya bakan bir dünya yazısı olarak çıkıyor: başlık büyük ve beyaz, değerler altında, eylem ([E] Silahı şarj et) sarı. Kutu yok, yazının koyu kenarlığı ve gölgesi var; yazı nesnenin önünde kalıyor, hafifçe süzülüyor ve kamera yakınlaşsa da ekranda aynı boyda duruyor. "Ağaç • Sağlık %86" gibi ilk satırlar başlık ve değer olarak ikiye ayrılıyor, uzun satırlar alta kayıyor. Başka bir nesneye bakınca eski yazı aşağı kayıp sönüyor, yenisi aşağıdan taşarak beliriyor. Aynı nesnede bir değer değişince yalnızca o satır hafifçe büyüyüp yeşil parlıyor; en fazla yarım saniyede bir, böylece sürekli artan bir depo titremiyor, nefes alıyor. Tarayıcı sonucu taranan yerin üstünde 8 sn kalıyor. İnşa modu durumu (maliyet, uyarılar) ve yeri belli olmayan mesajlar eski panelde kalıyor; menü, pazar ya da duraklatma açıkken yazı gizleniyor. Ayrıca sabah kartı seçilince kart listesi boşaldığı hâlde döngünün sonraki kartı okuyup IndexOutOfRange vermesi düzeltildi. **Doğrulama:** Play'de ağacın üstüne çağrılan yazı görüldü; küçük kaldığı için büyütüldü ve satırlar sarmalandı. Oyuncu oynarken şarj istasyonu ve batarya yazıları nesnelerin üstünde görüldü. Kart döngüsü düzeltmesi derlenmeden kaydedildi (oyuncu Play'deyken derleme tetiklenmedi) | Yapıldı |
| 28 Kasım | **Geri bildirim 12b: değerler sakin.** Oyuncu yazıyı beğendi ama sürekli değişimin sürdüğünü söyledi. Canlı ölçümde yazı 33 sn aynı nesnede duruyordu, yeniden belirmiyordu; değişen şey saniyede birkaç kez artan depo sayısıydı ve her artış satırı yarım saniyede bir parlatıyordu. Artık değer satırları (Üretim · Tüketim · Depo) sessizce en fazla 2 saniyede bir yenileniyor ve parlamıyor; arada gelen yeni değer sırasını bekliyor. Başlık ya da eylem değişince ("[E] Silahı şarj et" → "[E] Şarjı bırak") hemen gösteriliyor ve satır parlıyor. **Doğrulama:** oyuncu Play'deyken Unity derlemeyi Play bitene kadar beklettiği için yalnızca betik doğrulayıcısı çalıştı (hata yok); oyunda henüz görülmedi | Yapıldı |
| 28 Kasım | **Geri bildirim 12c: şarjda üst bildirim yok.** Oyuncu, şarj sırasında üstteki bildirimin sürekli değiştiğini gösterdi: elektrik gelip gittikçe "Araç bataryası şarj oluyor…" ile "Şarj bağlı · Elektrik bekleniyor…" arasında gidip geliyordu. Bu bildirim kaldırıldı; şarj durumunu istasyonun üstündeki dünya yazısı ("[E] Şarjı bırak") ve alet enerji çubuğu gösteriyor. Üst bildirim yalnızca gerçek olay mesajları için kalıyor. **Doğrulama:** oyuncu Play'deyken Unity derlemeyi Play sonuna bıraktığı için henüz derlenmedi ve oyunda görülmedi; değişiklik yalnızca bir metin seçiminin silinmesi | Yapıldı |
| 28 Kasım | **Geri bildirim 13: yanlış kurulan yapıyı geri alma.** Oyuncu yanlışlıkla koyduğu yapının öyle kaldığını söyledi; sökme (inşa modunda [X]) vardı ama görünmüyordu ve hep yarısını geri veriyordu. (1) **Tam iade:** kurulduktan sonraki 30 sn içinde sökülen yapı malzemenin tamamını geri verir; sonra eskisi gibi yarısı (GDD §9). Yüklenen kayıtlardaki yapılar yarım iade alır. (2) **Görünürlük:** inşa modunda kendi yapının üstüne gelince hayalet önizleme gizleniyor, yapı yarı saydam kırmızıyla nabız gibi parlıyor ve durum satırı "[X] Sök • +6 metal +3 plastik (tam iade / yarısı)" diyor. Kurulum mesajına "Yanlış yer mi? Üstüne gelip [X] • 30 sn içinde tam iade" eklendi. Oyuncunun kurmadığı yapılarda "Sök" artık yazmıyor. (3) **Efekt:** sökülen yapı kısaca esneyip yere gömülüyor, toz halkası ve kıvılcım çıkıyor, geri gelen malzeme yeşil "+6 metal / +3 plastik" olarak yükseliyor. **Doğrulama:** Play'de batarya kurulup hemen söküldü: 38/34 → 30/28 → 38/34 (tam iade). 30 sn'den eski güneş paneli üstüne imleç getirildi; kırmızı vurgu ve "(yarısı)" yazısı görüldü, sökünce +6/+3 geldi ve yeşil yazılar göründü. Kayıtlar geri yüklendi | Yapıldı |
| 28 Kasım | **Geri bildirim 13b: alet alanı yapıların üstünde de imleci izliyor.** Oyuncu, imleç bir yapının (şarj istasyonu) üstündeyken vakumun yeşil alanının imleci izlemediğini gösterdi. Sebep: yön, karakterin gövdesinden değil, elindeki aletin ucundan imlecin vurduğu noktaya çiziliyordu. Yapı karakterin hemen yanındayken aletin ucu o noktaya varıyor ya da geçiyordu; alan yana ya da ters yöne dönüyordu. Artık yatay yön gövdeden imlecin vurduğu noktaya ölçülüyor, yükseklik farkı aletin ucundan alınıyor; uzaktaki hedeflerde sonuç eskisiyle aynı. **Doğrulama:** Play'de karakter şarj istasyonuna yaslandı, imlecin istasyonun tepesinde olduğu durum hesaplandı. Eski yön (-0,24; -0,97) yaklaşık 75° yana kayıyordu, yeni yön (-0,99; -0,12) istasyona bakıyor. Sahte imleç konumu oyuna işlemediği için alan görüntüde denenmedi. Kayıtlar geri yüklendi | Yapıldı |
| 28 Kasım | **Geri bildirim 13c: alet yönü ekrandaki imleci izliyor.** Şarj istasyonunda düzelen alan, şehirdeki yüksek binalarda hâlâ yanlış yöne bakıyordu. Sebep: imleç bir binanın duvarında ya da çatısındayken vurduğu nokta, imlecin ekranda üstünde göründüğü yerden metrelerce uzakta (Depo C'de çatı 6 m yukarıda, yerdeki karşılığı 4–5 m kaymış). Artık yatay yön, imleç ışınının karakterin bel hizasındaki düzlemi kestiği noktadan alınıyor; yani ekranda karakterden imlece giden yön. Vakum ve geri dönüştürücü alanı, atış yönü, ateş ederken karakterin dönüşü ve elde tutulan aletin dönüşü aynı yönü kullanıyor. Otomatik savaş hedefi ve atışın yükseklik açısı eskisi gibi gerçek noktaya göre. **Doğrulama:** Oyuncu oynarken imleç Depo C binasındayken ölçüldü: ekranda karakterden imlece yön (0,78; −0,62), yeni yön (0,78; −0,62), eski yön (0,90; −0,44). Oyuncu Play'deyken derleme bekletildiği için kod henüz derlenmeden kaydedildi; betik doğrulamasında hata çıkmadı | Yapıldı |
| 28 Kasım | **Geri bildirim 12d: geri sayan yazı sakin.** Oyuncu, olta atınca "Balık bekleniyor… 0,8 sn" yazısının her an yeniden büyüyüp kaybolduğunu gösterdi: sayı başlıktaydı ve başlık değişince yazı yeni bir nesne sanılıp her karede yeniden beliriyordu. Artık yalnızca sayıları farklı olan başlıklar (geri sayım, sayaç) aynı yazı sayılıyor: sayı yerinde, sessizce değişiyor; büyüme, kayma ve parlama yok. Başka bir şeye bakınca geçiş efekti eskisi gibi. **Doğrulama:** Sayıları ayıklama denendi: "0,8 sn" ile "1,0 sn" aynı, "Ağaç" ile "Şarj istasyonu" farklı çıktı. Oyuncu Play'deyken derleme bekletildiği için olta ekranda denenmedi; betik doğrulamasında hata yok | Yapıldı |
| 28 Kasım | **Geri bildirim 12e: tabelalar alet alanının üstünde.** Oyuncu, Kirletici Giriş tabelasının ("[E] Sızıntıyı kapat • 10M + 5P") vakumun yeşil alanı üstüne gelince soluk ve okunmaz kaldığını gösterdi. Sebep: alet alanı en üst katmanda (sıra 4000) çiziliyor, dünya tabelaları ve bakılan nesnenin yazısı ise ondan önce (3000); yeşil dolgu yazının üstüne biniyordu. Artık tabelalar ve dünya yazısı alanın da üstünde (sıra 4010) çiziliyor; yeşil alan yazının altında kalıyor. Kaynak malzeme değiştirilmedi, çalışırken kopyası kullanılıyor. **Doğrulama:** Oyuncu Play'deyken derleme bekletildiği için ekranda denenmedi; betik doğrulamasında hata yok | Yapıldı |
| 28 Kasım | **Geri bildirim 14: otomatik kayıt görünür.** Oyuncu otomatik kayıt istedi; sistem zaten vardı (her 3 dk aktif oyunda ve kart seçimi, gün sonu, sipariş, final gibi anlarda; dönüşümlü 3 yuva, duraklatmada ve bayılınca yazmaz) ama sessiz çalıştığı için fark edilmiyordu. Artık otomatik kayıt yazılınca sağ altta küçük bir "Otomatik kaydedildi" hapı beliriyor: yeşil nokta nabız gibi atıyor, 2 sn sonra sönüyor. Üstteki bildirimlere dokunmuyor; elle ve hızlı kayıtta eski mesaj sürüyor. **Doğrulama:** Oyuncu Play'deyken derleme bekletildiği için ekranda denenmedi; betik doğrulamasında hata yok | Yapıldı |
| 28 Kasım | **Geri bildirim 15: 3D ana menü.** Eski pencere menüsünün yerine canlı bir sahne geldi. Sağda bulanık ağaçlı yeşil bir açıklıkta karakter elinde vakumla duruyor; gövdesi, kolları ve vakum fareye dönüyor. Solda ekranın yarısını kaplayan panel soldan sağa %100'den %30'a inen siyah gradyanla çiziliyor. Butonlar yeşil yaprak çerçeveli, içi ten rengi kartlar ve üstüne gelince hafif büyüyüp kıpırdıyorlar. Boş yerde basılı tutunca vakum havadaki zerreleri çeken efekti çalıştırıyor, pervanesi dönüyor. Bir butona basınca vakum butonu çekip yutuyor. Kayıtlar ve Ayarlar gibi panelli seçeneklerde karakter ekranın ortasına bir kart fırlatıyor, kart açılınca panel görünüyor. Panel kapanınca kart yeniden vakuma çekiliyor ve buton yerine geri uçuyor. Yeni oyun, Devam et ve öğreticiler emişten sonra sahneyi kapatıp oyunu başlatıyor. Menü duraklatılmış oyunda çalıştığı için tüm animasyonlar ölçeksiz zamanla oynuyor. **Doğrulama:** Play'de ekran görüntüsüyle sahne, butonlar ve "Kayıtlar" aç/kapa akışı denendi; gerçek fareyle basılı tutma oyuncu testine kaldı | Yapıldı |
