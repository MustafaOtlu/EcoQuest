# EcoQuest uygulama ve doğrulama planı

Kaynak: GDD ECO.docx ve 24–25 Eylül 2026 tarihli kullanıcı kararları. Hedef bu listenin tamamıdır. Aşamalar bağımlılık sırasıyla uygulanır; bir aşamanın bitmesi bütün oyunun tamamlandığı anlamına gelmez.

## Sabit tasarım kararları

- Oyuncu ölmez veya başlangıca ışınlanmaz. Karakter enerjisi bitince bulunduğu yerde beş saniyede toparlanır. Bu sırada tekrar hasar ve araç kullanımı engellenir; envanter korunur.
- Karakter enerjisi ile silahların kullandığı elektrik bağımsızdır. Silah elektriği üretim, depolama ve şarjla sağlanır.
- Aynı haritada yaklaşık dengeli dağılan sağlıklı ve kirli alanlar vardır; çevre bölgesel değişir.
- Harita düzeni mekaniklerden sonra yapılır. Kapsamlı performans ve uçtan uca dağıtım testi en son aşamadır; geliştirme boyunca ilgili işlevlerin çalışırlığı kontrol edilir.
- Mevcut çalışma ve kullanıcı değişiklikleri korunur. Ücretsiz varlıkların kaynak ve lisansları saklanır.

## Aşamalar

1. [x] Karakter enerjisi, beş saniyelik toparlanma, kontrol/saldırı kilidi ve ayrı HUD göstergeleri.
2. [x] Elektrik üretimi, gün/gece ve hava etkisi, depolama, bağlantı ve silah şarjı.
3. [x] Metal/plastik/organik kaynak zinciri, gübre, YEP kazanımı, seviyeler ve kilitler.
4. [x] İnşa önizlemesi, yerleştirme, döndürme, maliyet, sökme, tamir; kablo ve boru bağlantıları.
5. [x] Güneş paneli, rüzgâr türbini, batarya/şarj, su arıtma, depo, geri dönüşüm tesisi, tarla; model ve etkileşimleri.
6. [ ] Market, demir top satın alma, tohum/filtre alışverişi, YEP ile araç geliştirmeleri ve dengeli maliyetler.
7. [ ] Bölgesel hava/su/toprak durumu, karbon etkisi, sıcaklık, yangın, yanlış kurulumun sonuçları; fabrikalar ve filtreleri.
8. [ ] İklime bağlı farklı ürünler, sulama, gübre, zararlılar, ağaçlandırma, kuş/kelebek dönüşü ve balıkçılık.
9. [ ] Düşmanların çevre/yapı hedefleri, yol bulma, doğma kuralları, tenekeye filtre ve iklim canavarı.
10. [ ] Ev/çocuk bakımı, beslenme/temiz su, hikâye görevleri, bölge açılışları ve tamamlanma akışı.
11. [ ] Konum, envanter, enerji, yapılar/hatlar, üretim/depolar, bitkiler, çevre, YEP, düşman ve görev/bakım durumunun tam kaydı.
12. [ ] Silah/çarpma/çevre/tesis sesleri, vuruş ve etkisiz kalma tepkileri, tıkanma/düşük şarj geri bildirimi.
13. [ ] İnşa, market, envanter, YEP, üretim ve bölge ekranları; ana menü, duraklatma, kayıt yükleme, ayarlar ve öğretici.
14. [ ] Mekanikler tamamlanınca şehir, sanayi, güneşli/rüzgârlı alan, iki göl, buzul, yollar ve iyi/kötü alan yerleşimi.
15. [ ] En son çoklu düşman/yapı/bitki yükü, kapsamlı optimizasyon, uçtan uca oyun, çözünürlükler ve bağımsız Windows çıktısı.

Baraj GDD'de ayrıntılı bir sistem değil, yanlış kurulum örneğidir; ayrı bir genişletme olarak tutulur.

## İlerleme

- Başlangıç durumu: silahlar, basit düşmanlar, ekim/hasat, HUD ve küçük saha istasyonu mevcut. Önceki testler bu yeni listenin tamamlandığını kanıtlamaz.
- 25 Eylül: 1. aşama MainScene üzerinde 15 Unity Play Mode kontrolünden geçti. Beş saniye boyunca kademeli dolum, yeniden hasarın engellenmesi, konum/yön/envanterin korunması, silahların kilitlenmesi ve HUD ayrımı doğrulandı. Kontrol kaynağı: Tools/UnityChecks/RecoveryCheck.cs.
- 25 Eylül: 2. aşamada gündüz/gece, bulut/gölge/rüzgâr, depolama, kablo döngüsü/kopması, yetersiz elektriğin paylaştırılması ve gerçek MainScene şarj etkileşimi PowerCheck ile geçti. Görüntüler PowerVisualCheck ile render edildi; karakter ve silah göstergeleri ayrı ve okunabilir.
- 25 Eylül: 3. aşamanın kaynak/organik/gübre/YEP hesapları ResourceProgressionCheck ile 40 kontrolden geçti. Yapı kilitleri ve tesis kademeleri 26 Eylül inşa/üretim aşamasında bağlandı. Market geliştirmeleri ayrı 6. aşamadadır.
- 25–26 Eylül: ConstructionCheck 26 kontrolle yerleştirme, seviye/malzeme sınırları, tamir, sökme ve elektrik kablolarını doğruladı. Su borusu ve depo/arıtma zinciri WaterInfrastructureCheck ile; geri dönüşüm, tarla ve 2/6/12 ile 4/12/24 kademeleri FacilityCheck ile Unity Play Mode içinde doğrulandı. İçerikler ve malzemeler tekrar etkileşimle çoğalmıyor. FacilityVisualCheck modelleri, renkli boruları ve sayfalı HUD'u render etti. Ayrıntılar: ConstructionAndProduction.md.
- Sıradaki: 6. aşama — market ve silah geliştirmeleri. Çevre, tam dünya kaydı ve sonraki aşamalar henüz tamamlanmadı. Harita ve kapsamlı son testler planın sonunda kalıyor.
