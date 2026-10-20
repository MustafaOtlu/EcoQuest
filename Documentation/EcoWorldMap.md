# EcoWorld haritası — kurulum ve sistemler (150 × 150 m)

Bu belge `EcoWorld.unity` sahnesini, onu üreten editör betiğini ve haritaya bağlı arayüzü anlatır.
`MainScene` değiştirilmez; EcoWorld onun kopyası üzerinden kurulur ve her an yeniden kurulabilir.

## Sahneyi (yeniden) kurmak

- Menü: **EcoQuest ▸ Build EcoWorld map scene (no prompts)** — soru sormaz, sahneyi kaydeder.
- Menü: **EcoQuest ▸ Build EcoWorld map scene** — üzerine yazmadan önce sorar.
- Betik: `Assets/Scripts/Editor/BuildEcoWorldMap.cs`. Rapor: `Assets/EcoWorld/BuildReport.txt` ve konsolda `ECOWORLD_READY`.
- Kopyadaki eski dekor (arazi, evler, yollar, lambalar…) silinir; `Daylight`, `Cam's`, `Enemys`, `Player` ve `alan` (üs) korunur.

## Zemin: Terrain yok

- Unity Terrain kullanılmaz. Zemin, 1,5 m hücreli ve 4×4 parçalı **alçak poligon bir mesh**tir (`Assets/EcoWorld/Meshes/EcoGround_*`).
- Renkler köşe renkleriyle boyanır; `EcoQuest/LowPoly Vertex Lit` shader'ı (`Assets/Shaders/EcoLowPoly.shader`) yüzey gölgelendirmesi, gölge, gece lambaları ve sisle çizer.
- Her parçada `MeshCollider` ve `EcoGround` vardır. Yüzey türleri: **Soil** (ekim ve inşa), **Road**, **Plaza**, **Rock**. Yolda ve meydanda ekim veya inşa yapılmaz; oyuncuya neden açıklanır.
- Oynanabilir alanın dışında tepeler, sınır ormanı ve kuzeyde dağlar vardır. Kenarlardaki görünmez duvarlar oyuncuyu içeride tutar.
- **Kirlilik görünür:** `EcoPollutionField`, haritanın üstüne küçük bir kirlilik dokusu yazar (toprak, hava, su ve eksik bitki örtüsü). Zemin bu dokuya göre soluk kahve-griye döner; bölge temizlendikçe yeşil geri gelir.

## Yollar: ızgara yok

Yollar Catmull-Rom spline'ları boyunca üretilen şerit mesh'lerdir ve yumuşatılmış bir yükseklik alanını izler.

| Tür | Görünüş |
| --- | --- |
| Ana cadde | 6 m asfalt, çift sarı orta çizgi, bordür ve kaldırım |
| Sokak / bulvar | 4,4 m asfalt, kesikli orta çizgi, bordür ve kaldırım |
| Kır yolu | Çakıl banket ve kenar çizgileri |
| Sanayi yolu | Tozlu asfalt |
| Patika | Düzensiz kenarlı toprak, tekerlek izleri |
| Dere | Temiz (dağ deresi → Batı Gölü) ve kirli (atık su kanalı → Doğu Gölü) su şeritleri |

Kavşaklarda çizgiler ve kaldırımlar kesilir, şehir sokaklarına yaya geçidi çizilir. Merkezde çeşmeli bir **göbekli kavşak** vardır. Dört yan sokak (Çınar, Lale, Okul, Fırın) ana çaprazdan farklı açılarla ayrılır; evler sokağa bakar ve iç parsellere ikinci sıra evler yerleşir.

## Ölçek ve bölgeler

Tek sabit: `Size` (varsayılan `150`). Bütün yerleşim `L = Size / 150` çarpanıyla ve `P(x, z)` yardımcısıyla yazılmıştır.

| Bölge | Merkez (x, z) | Boyut | Proje | Not |
| --- | --- | --- | --- | --- |
| Güneşli Çayır | (75, 25) | 62 × 50 | P1 | Üs (75, 16), güneş tarlaları, ilk üretim |
| Batı Gölü | (22, 25) | 44 × 50 | — | Temiz su, balık, doğal akış (hidro) |
| Doğu Gölü | (128, 25) | 44 × 50 | P2 | Kirletici giriş, kirli kanal, Salya |
| Rüzgârlı Tepeler | (22, 80) | 44 × 60 | — | Türbin sırtları (rüzgâr ×1,6) |
| Koruluk | (22, 99) | 40 × 20 | P3 | Hasarlı orman, hassas habitat (öncelik 1) |
| Şehir Merkezi | (75, 80) | 62 × 60 | P5 | Ev ve çocuk, pazar, kütüphane, sağlık ocağı |
| Sanayi Bölgesi | (128, 80) | 44 × 60 | P4 | İki işlevsel fabrika, depolar, atık sahası |
| Buzul Bölgesi | (75, 130) | 150 × 40 | P6 | Buz kütleleri, patika, izleme istasyonu (96, 133) |

Üssün içindeki iki deneme bölgesi `Üs bahçesi` ve `Eski atölye sahası` adını alır ve 14 × 14 m'ye küçültülür; böylece büyük bölgelerin önüne geçmezler.

## Kamera ve kontrol

Ortografik izometrik kamera (`EcoIsoCamera`), yarı yükseklik 10 m (tekerlekle 6–16). WASD hareket, sağ tık NavMesh rotası, fare ile nişan.
`[B]` inşa, `[M]` harita, `[J]` proje günlüğü, `[K]` uzmanlık, `[O]` siparişler (pazarda), `[E]` etkileşim, `[F]` final,
`[F1]` kontroller, `[F2]` ses, `[H]` arayüz modu, `[F5]`/`[F9]` kayıt, `[Esc]` panel kapat / duraklat.

## Arayüz

- **İkonlar:** `Tools/IconArt/icons.js` içinde elle çizilmiş 85 SVG ikon; `Assets/Resources/EcoIcons/` altına PNG olarak çizilir. Yeniden üretmek için:
  `node Tools/IconArt/icons.js <çıktı-klasörü> 128` (`@resvg/resvg-js` gerekir). `EcoIcons.Get("water")` ile yüklenir.
- **HUD kartları** (`EcoQuestHUD`, `EcoWorldHUD`, ortak kit `EcoUI`): Durum, Çevre, Harita, Kaynaklar ve Hedef kartları başlıktaki ok ile katlanır. Seçim `PlayerPrefs`'e yazılır.
- **[H] arayüz modu:** Tam → Sade (yalnız özet satırları) → Gizli (yalnız arayüz düğmesi).
- **Hızlı erişim:** sol altta harita, günlük, inşa, uzmanlık (hak varken rozet gösterir), ses, yardım ve arayüz düğmeleri.
- **[M] dünya haritası:** haritanın üstten çekilmiş görüntüsü (`Resources/EcoWorld/Minimap.png`). Özellikleri:
  - Tekerlekle yakınlaştırma, sürükleyerek kaydırma, tıklayınca bölgeyi sabitleme.
  - Bölge sınırları ve canlı hava simgeleri: rüzgârlı yerde sallanan rüzgâr, yağmur, kar, kirli havada duman.
  - İlgi noktaları: pazar, ev, fabrika, istasyon, giriş, hidro, şarj, yangın ve düşman.
  - Farenin altındaki bölge için bilgi kartı: göstergeler, iklim (sıcaklık, rüzgâr, güneş, hava), canlı görevler (proje ilerlemesi, yangın, düşman, filtre, susuz fidan, atık, balık) ve fırsatlar.
- **IMGUI pencereleri** (menü, günlük, uzmanlık, siparişler, ses, pazar) ikonlu temayı (`EcoTheme.Window/IconButton/Icon`) kullanır. Bu pencereler açıkken uGUI HUD gizlenir.

## Oyun sistemleri (EcoWorld'e eklenenler)

| Sistem | Betik | Kısaca |
| --- | --- | --- |
| Projeler | `EcoProjects` | Altı proje, 120 sn doğrulama, her biri final hazırlığının 1/6'sı; koruluk projesi gerçek `Koruluk` bölgesini okur |
| Buzul | `EcoGlacierStation`, `EcoGlacierMelt` | İstasyon **yalnız ölçer**; buz, alan ağırlıklı dünya karbonu (`EcoRegion.WorldCarbon`) 20'nin üstündeyken erir, altında yavaşça toparlanır |
| Pazar / siparişler | `EcoMarket`, `EcoMarketUI`, `EcoOrders` | Günlük alım sınırı, temiz balık takası, türe göre siparişler |
| Final | `EcoFinale` | Buzul istasyonundaki terminal, üç odak |
| Kayıt | `EcoWorldSave` | 3 elle + 1 hızlı + 3 otomatik slot, yedekli yazma |
| Çocuk | `EcoStory` | Evin kapısında bekler (`Eco Home door`), üç bakım anı |
| Harita işaretleri | `EcoMapMarker` | Kurucunun ve oyun betiklerinin haritaya eklediği ilgi noktaları |
