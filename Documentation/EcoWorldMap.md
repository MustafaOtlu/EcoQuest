# EcoWorld haritası — kurulum ve sistemler (200 × 200 m)

Bu belge `EcoWorld.unity` sahnesini, onu üreten editör betiğini ve bu sahneye eklenen oyun sistemlerini anlatır.
`MainScene` değiştirilmez; EcoWorld onun kopyası üzerinden kurulur ve yeniden kurulabilir.

## Sahneyi (yeniden) kurmak

- Menü: **EcoQuest ▸ Build EcoWorld map scene (no prompts)** — soru sormaz, sahneyi kaydeder.
- Menü: **EcoQuest ▸ Build EcoWorld map scene** — üzerine yazmadan önce sorar.
- Betik: `Assets/Scripts/Editor/BuildEcoWorldMap.cs`. Rapor: konsolda `EcoWorld oluşturma raporu`.
- Kopyadaki eski haritadan kalan yinelenen nesneler (ikinci şarj istasyonu, uzak türbinler, pompa vb.) kurulum sırasında silinir; yalnız
  `Daylight`, `Cam's`, `Enemys`, `Player` ve `alan` (üs) korunur.

## Ölçek

Tek sabit: `Size` (varsayılan `200`). Tüm yerleşim `L = Size / 200` çarpanıyla ve `P(x, z)` yardımcısıyla yazılmıştır;
`Size` değiştirilince bölgeler, yollar, şehir ve sahalar orantılı ölçeklenir. NavMesh hacmi ve voksel boyutu `EcoNavigation` içinde
`Size`'a göre ayarlanır.

| Bölge | Merkez (x, z) | Boyut | Not |
| --- | --- | --- | --- |
| Üs | (100, 24) | — | Oyuncu başlangıcı, şarj istasyonu, düşmanlar |
| Güneşli Alan | (100, 37) | 76 × 74 | Güneş sahaları |
| Batı Göl / Doğu Göl | (31, 37) / (169, 37) | 62 × 74 | Göller, balık noktaları |
| Rüzgâr Çiftliği | (30, 110) | 60 × 72 | En çok 5 türbin |
| Şehir | (100, 110) | 80 × 72 | 4×4 blok, ~15 bina, meydan, lamba, trafik ışığı |
| Sanayi | (170, 110) | 60 × 72 | Fabrikalar |
| Buzul | (100, 173) | 200 × 54 | Buz blokları, patika, izleme istasyonu |

Buzul patikası `TerrainHeight` içinde yumuşak bir rampa koridoru olarak çizilir (yaklaşık %20 eğim); istasyon platformu (122, 164).
Zemin bir `Terrain` nesnesidir; yükseklik 0–130 m, su seviyesi y = 17,5.

## Kamera ve kontrol

İzometrik kamera (`EcoIsoCamera`), WASD hareket, sağ tık NavMesh rotası (`PlayerController`), fare ile nişan, tekerlek yakınlaştırma,
Esc duraklatma. `[B]` inşa modu, `[M]` harita/HUD, `[J]` proje günlüğü, `[O]` sipariş panosu (markette), `[E]` etkileşim,
`[F]` final, `[K]` uzmanlık, `[F2]` ayarlar, `[F5]` kaydet, `[F9]` yükle.

## Oyun sistemleri (EcoWorld'e eklenenler)

| Sistem | Betik | Kısaca |
| --- | --- | --- |
| Projeler | `EcoProjects` | Altı proje (enerji, göl, koru, sanayi, yerleşim, buzul), 120 sn doğrulama, her biri final hazırlığının 1/6'sı |
| Buzul istasyonu | `EcoGlacierStation`, `EcoGlacierMelt` | `[E]` ile çalışır; ortalama karbon ≤ 20 iken erime çizgisi geri çekilir |
| Pazar | `EcoMarket`, `EcoMarketUI` | Günlük alım sınırı, ganimet sınırsız, açıkken zaman %10 hızda |
| Siparişler | `EcoOrders` | Her 3. gün 2 sipariş, ≈%25 avantaj, tek teslim, süre cezası yok |
| Final | `EcoFinale` | Hazırlık ≥ %50 iken `[F]`; proje sayısı canavarı zayıflatır; kayıp dünyayı sıfırlamaz |
| Kayıt | `EcoWorldSave`, `PlayerWeaponSystem` | `[F5]`/`[F9]` ve 3 dk otomatik kayıt; envanter + dünya durumu ayrı anahtarlarda |
| Bayılma | `PlayerVitals` | Silah elektriğinin ≈%20'si, 3 sn dokunulmazlık, düşmanlar geri itilir |
| Uzmanlık | `EcoSpecialization` | `[K]`: Enerji / Su / Ekoloji, 3 kademe; YEP seviyesi/2 kadar hak
| Ses | `EcoAudio` | Üretilmiş ortam sesleri (rüzgâr, su, yağmur, sanayi, kuş) ve olay sesleri; `[F2]` ses ayarı
| Öğrenme kartları | `EcoProjects` | Proje tamamlanınca 'Biliyor muydun?' kartı

## Kayıt kapsamı

Kaydedilir: oyuncu konumu, gün/saat, bölge kirlilik ve bitki değerleri, oyuncu yapıları (kademe, bütünlük), kablolar, borular,
ekili bitkiler, proje/YEP ilerlemesi, sipariş teslimleri, buzul istasyonu, final zaferi, envanter ve yükseltmeler.
Kaydedilmez: düşmanlar (yüklemede başlangıç durumuna döner), depo içerikleri, devam eden final dövüşü.
Yükleme ödül çoğaltmaz ve sipariş yeniden üretmez.

## Bilinen sınırlar / yapılacaklar

- Final dövüşünde üç baskı var: enerji kesintisi (45 sn'de bir, 12 sn), kuraklık ve duman dalgası (60 sn'de bir).
- Erişilebilirlik, ses/müzik, dil seçeneği ve performans hedefi GDD §15.2'de açık.
- Blender ile üretilmiş yeni modeller yok; mevcut prefablar yeniden kullanılıyor.


## Uygulama durumu (otomatik güncelleme)

| Madde | Durum |
|---|---|
| Derleme, EcoWorld sahnesinin menüden yeniden kurulması, EditorBuildSettings sırası (EcoWorld önce) | Uygulandı |
| EcoSettings (arayüz ölçeği %100/125/150, renk körü paleti), başlangıç ekranı (yeni oyun/devam/slotlar/ayarlar) ve duraklatma menüsü, 3 manuel slot + hızlı + final öncesi slot arayüzü | Uygulandı |
| Ses: ana/müzik/ortam/efekt/arayüz ayrı sürgüleri, sessiz mod, dünya sağlığına göre değişen prosedürel müzik | Uygulandı (müzik sentezlenmiş, sanatçı yapımı değildir) |
| EcoStory: mahalle çocuğu, 3 bakım anı (20 YEP), 3 saha görevi (20 YEP) | Uygulandı (AwardOnce kimlikleriyle) |
| Final: buzul istasyonundaki terminal, 120 EB/60 SB rezerv (kayda yazılır), 3 odak, yenilgide sıfırlama, %100 hazırlıkta %25 düşük gereksinim, baskıların örtüşmemesi, final öncesi güvenli kayıt, şablon yoksa zafer verilmez | Uygulandı |
| Buzul istasyonu elektrik talebi ve erime hızı ölçümü | Uygulandı (güç düğümü yoksa güçlü sayılır) |
| Tuş yeniden atama (WASD, koş, etkileşim, mod, inşa, günlük, harita vb.), basılı tutma/aç-kapat seçeneği | Uygulandı (ayarlar > tuş atamaları; çakışmada takas; ekrandaki ipuçları varsayılan harfleri gösterir) |
| Kamera sarsıntısı ve bulanıklık/hasar vurgusu anahtarları | Uygulandı (EcoCameraFx; hasarda sarsıntı + kenar vurgusu, ayardan kapatılır) |
| Renk körü paleti | Uygulandı (menü, final, proje çubuğu, HUD göstergeleri) |
| 5 soruluk ön/son anket ve performans ölçümü | Uygulandı (EcoSurvey: yerel JSON; EcoPerf: F3, FPS/alt %1/bellek, CSV) |
| MainScene bağlantıları | Bilerek yapılmadı: MainScene'e dokunulmaz; oyun EcoWorld'den çalışır |
| Gerçek klavye/fare hissi | Test edilemedi; gerçek oyun denemesi gerekir |
| Doğu Gölü kirletici girişi ve Batı Gölü doğal akış (hidro) alanı sahnede | Uygulandı (builder; giriş [E] ile kapanır, proje durumu bunu okur) |
| Final terminal rezervi kayıt/yükleme (global kanca) | Uygulandı ve doğrulandı |
| Saha görevleri (hasat/yeniden ekim/balık) | EcoDiscoveries ödüllendirir; EcoStory yalnız 3 bakım anını ödüllendirir |
| Konsol uyarıları: "The tree … Nature/Soft Occlusion" | Bilinen sınır: kullanıcının Assets/Terrains/New Terrain.asset dosyasından geliyor (MainScene arazisi); dokunulmadı |
