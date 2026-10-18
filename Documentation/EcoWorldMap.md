# EcoWorld haritası (yeni sahne)

18 Ekim 2026. `Assets/Scenes/MainScene.unity` **değiştirilmedi**. Yeni harita `Assets/Scenes/EcoWorld.unity` olarak, Unity menüsündeki **EcoQuest > Build EcoWorld map scene** komutuyla üretilir (tekrar çalıştırılabilir; yalnız EcoWorld yenilenir).

## GDD'ye göre yerleşim (640 × 640 m)

| Bölge | Konum | Durum |
| --- | --- | --- |
| Buzul Bölgesi | kuzey | temiz, hassas; karbon arttıkça buz blokları küçülür (`EcoGlacierMelt`) |
| Rüzgârlı Alan | batı | temiz; tepelerde 11 dekoratif türbin (rüzgâra göre döner) |
| Şehir | orta-kuzey | orta kirlilik; bina ızgarası, meydan, sokak lambaları |
| Sanayi Bölgesi | kuzey-doğu | kirli; 2 filtre takılabilir fabrika (`EcoFactory`), 2 eski depo, tank/konteyner/atık |
| Güneşli Bölge | orta-güney | temiz; iki güneş paneli sahası, başlangıç üssü |
| Batı Gölü Havzası | güney-batı | temiz göl, iskele, balık tutma |
| Doğu Gölü Havzası | güney-doğu | kirli göl (arıtma gerekir), iskele |

İyi/kötü dağılım: 4 temiz, 1 orta, 2 kirli bölge. İki ayrı göl (GDD notu) vardır; su rengi bölgenin su kirliliğine göre değişir (`EcoLakeTint`).

## Arayüz

- `EcoWorldHUD`: GDD düzeltme #12'deki 5 gösterge (hava kalitesi, su kalitesi, karbon ayak izi, biyoçeşitlilik, ekosistem sağlığı) + sıcaklık, bulunulan bölge adı.
- Minimap (sağ üst) ve **[M]** ile tam ekran dünya haritası; bölge girişinde banner; açılış başlığı.
- Mevcut `EcoQuestHUD` paneli yuvarlak köşeli sprite ile (opsiyonel `panelSprite`/`slotSprite` alanları) güzelleştirildi.
- Biyoçeşitlilik/ekosistem sağlığı oyun endeksleridir: bitki örtüsü, kuş-kelebek dönüşü ve kirlilik değerlerinden türetilir; simülasyonu değiştirmez.

## Teknik notlar

- Sahne MainScene kopyası olduğundan oyuncu, silahlar, düşmanlar, saat ve istasyon bağlantıları aynen çalışır. Eski arazi/ev/yol/lamba/direk/güneş dizileri kopyadan çıkarıldı; ev, lamba, direk ve güneş dizisi prefabları yeniden kullanıldı.
- `alan` grubu (oyuncu + deneme istasyonu) güneşli bölgede (320, 70) noktasına taşındı; içindeki küçük bölgeler öncelik 5 ile korunur.
- Dekoratif ağaçlar `EcoVegetation` taşımaz (aksi halde bölge kirliliğini çok hızlı temizlerdi). Bölge başına yalnızca 3 canlı ağaç vardır; kuş/kelebek onlarla döner.
- Rapor: `Assets/EcoWorld/BuildReport.txt`. Üretilen varlıklar `Assets/EcoWorld/` ve `Assets/Resources/EcoWorld/` altındadır.

## Henüz yapılmadı

- Rüzgâr/güneşin bölgeye göre farklılaşması (EcoWorldClock genel değerleri kullanır).
- Tam dünya kaydı ve performans testi (yol haritası 11 ve 15. aşama).
- Ana menüden EcoWorld'e geçiş; sahne Build Settings listesine eklenir ama menü butonu bağlanmadı.
