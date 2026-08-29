# Eco Quest - Oyun Tasarım Belgesi (GDD)

## Proje Bilgileri
- **Proje:** TÜBİTAK Eco Quest
- **Ekip:** Mustafa, Hanefi, Bilge
- **Motor:** Unity (2D Top-Down, Piksel Art, Tile-based)
- **Platform:** Windows PC
- **Teslim:** 11 Eylül 2026 (%70)

## Oyun Özeti
Oyuncu, çevresel sorunlarla karşı karşıya olan bir şehirde yaşamaktadır. Temel amaç: hava, su, sıcaklık, atık ve ekosistem göstergelerini takip ederek çevresel dengeyi korumak ve gelecek nesiller için yaşanabilir bir çevre oluşturmak.

## Teknik Kararlar
| Karar | Seçim |
|---|---|
| Perspektif | 2D Top-Down |
| Görsel Stil | Piksel Art |
| Yerleştirme | Tile-based (ızgara) |
| Zaman Sistemi | Real-time (gün/gece döngüsü) |
| Oyuncu Kontrolü | WASD hareket + bina modunda kamera kaydırma |
| Harita | 6 bölge, hepsi açık, mekanik kilitleriyle sınırlı |

## Çekirdek Döngü
Çevresel göstergeleri takip et → Sorunu belirle → Kaynakları ve uygun çözümü seç → Müdahale et → Çevredeki değişimi gözlemle → Çocuğun ve ekosistemin tepkisini gör → Kaynak/ekipmanlarını geliştir → Yeni çevresel sorunlara hazırlan.

## 5 Temel Gösterge (HUD)
1. **Hava Kalitesi** (0-100)
2. **Su Kalitesi** (0-100)
3. **Karbon Ayak İzi** (0-100, düşük = iyi)
4. **Biyoçeşitlilik** (0-100)
5. **Ekosistem Sağlığı** (0-100)
6. **Sıcaklık** (-20 ile 60 arası)

## Harita Bölgeleri
1. **Buzul** (üst) - Kutup buzulları, iklim değişikliğine duyarlı
2. **Rüzgarlı Alan** (sol) - Rüzgar türbinleri için ideal
3. **Şehir** (orta-üst) - Ana yaşam alanı, ev burada
4. **Güneşli Bölge** (orta-alt) - Güneş panelleri için ideal
5. **Sanayi Bölgesi** (sağ-üst) - Fabrikalar, kirlilik kaynağı
6. **Temiz Su Kaynağı** - Sınırlı su kaynağı

## YEP (Yenilenebilir Enerji Puanı / Ekolojik Denge Puanı)
- Maksimum 48 seviye
- Çevresel göstergelerin iyileşmesiyle kazanılır (sadece bina kurmak puan vermez)
- Bina ve ekipman kilitleri YEP seviyesine bağlı

## Binalar
| Bina | Metal | Plastik | YEP Seviye | Özellik |
|---|---|---|---|---|
| Güneş Paneli | 256 | 128 | Her seviyede 2 üretim hakkı | Gündüz 2kWh |
| Rüzgar Türbini | 2560 | 1280 | 4. seviyede açılır, her 3 seviyede 2 hak | Rüzgarlı alanda 120kWh/gün |
| Geri Dönüşüm Tesisi (K/O/B) | 128-512 | 64-256 | 2/6/12 | Otomatik dönüşüm |
| Su Arıtma (K/O/B) | 64-256 | 64-256 | 4/12/24 | Su temizleme, maks 8 |
| Su Deposu (K/O/B) | - | 64-256 | 4/12/24 | Su depolama, maks 8 |
| Tarla | - | 24-64 | Açık, tohumlar YEP ile açılır | Yemek üretimi |
| Kablo | 1/m | 1/m | - | Elektrik taşıma |
| Boru | - | 1/m | - | Su taşıma |

## Düşmanlar
| Düşman | Özellik | Etkisizleştirme |
|---|---|---|
| Teneke Canavarı | Yavaş, dayanıklı, her şeye saldırır, duman canavarı üretir | Geri dönüşüm |
| Alev Ruhu | Binaları eritir, ormanları yakar | Su ile söndürme |
| Duman Canavarı | Güneş paneli verimini düşürür, görüşü kısıtlar | Filtre + temiz enerji |
| Salya Canavarı | Suda bulunur, balıkları zehirler, su arıtmayı yavaşlatır | Su arıtma + atık temizliği |
| İklim Canavarı (Boss) | Karbon ayak izine göre güçlenir, buzulları eritir | Tüm göstergeleri iyileştir |
| Asit Yağmuru | Ekinlere ve binalara hasar | Hava kalitesini iyileştir |

## Ekipmanlar
| Ekipman | İşlev | Modlar |
|---|---|---|
| Vakum Silahı | Eşya toplar, su fırlatır | Vakum / Anti-vakum / Su fırlatma |
| Tohum Silahı (Demir Sapan) | Hızlı tohum dikme, demir top fırlatma | - |
| Geri Dönüşüm Silahı | Eşyaları yerinde dönüştürür (yavaş) | - |
| Tarama Cihazı | Rüzgar/güneş potansiyelini gösterir | - |

## Çocuk Karakter (Gelecek Nesil Temsilcisi)
Evde yaşar, çevresel koşullara tepki verir:
- Hava kirliyse → öksürür
- Sıcaklık yükselirse → sıcaktan rahatsız olur
- Çevre yeşillenirse → mutlu olur, kuş seslerinden söz eder
- **Açlık/su mekanikleri YOK** - sadece çevresel geri bildirim

## Kaynak Döngüsü
- **Metal:** Hazır bulunur, çöp geri dönüşümü, teneke canavarı ödülü
- **Plastik:** Hazır bulunur, çöp geri dönüşümü
- **Temiz Su:** Sınırlı kaynak → arıtma → depolama → kullanım
- **Tohum:** YEP ile açılır, iklime göre ekim

## Biyoçeşitlilik Zincirleri
- Ağaçlandırma ↑ → kuş/kelebek ↑ → biyoçeşitlilik ↑
- Kuş popülasyonu ↑ → zararlı böcek ↓ → tarım verimi ↑
- Su kalitesi ↑ → balık popülasyonu ↑ → ekosistem sağlığı ↑

## İklim Değişikliği Zincirleri
Fosil/kirletici faaliyet ↑ → Karbon ayak izi ↑ → Sıcaklık ↑ → Buzullar ↓ → Su dengesi bozulur → Tarım verimi ↓ → Yangın riski ↑
