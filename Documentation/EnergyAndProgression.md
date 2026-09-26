# Karakter enerjisi elektrik ve kaynak ilerlemesi

## Karakter enerjisi

Oyuncunun can/ölüm/yeniden doğma döngüsü kaldırıldı. Düşman darbeleri karakter enerjisini azaltır. Sıfıra düşünce oyuncu olduğu yerde beş saniye boyunca kademeli olarak toparlanır; hareket, araç kullanımı ve yeni darbeler bu süre boyunca engellenir. Enerji henüz kısmen dolmuşken kontrol geri gelmez. Konum, bakış, envanter ve silah bataryası korunur. Süre oyun zamanı kullanır; ileride duraklatma aynı zamanı durduracaktır.

`PlayerVitals.maximumHealth` sahne ayarları `FormerlySerializedAs` ile `maximumEnergy` değerine aktarılır. Eski ölüm API'si yerine `IsRecovering`, `Energy` ve `RecoveryRemaining` kullanılır.

## Elektrik

MainScene'deki PowerStarter prefabı bir güneş paneli, rüzgâr türbini, batarya, şarj terminali ve üç kablo içerir. Başlangıç bataryasında 80 birim vardır; Battery yapı prefabının kendi başlangıç değeri sıfırdır. Üretim değerleri oyun birimidir, gerçek kWh iddiası taşımaz.

- Güneş: 5 birim/sn tepe üretim; günün saati, bulutluluk, konum katsayısı ve fiziksel gölgeye bağlıdır. Gece üretmez.
- Rüzgâr: 8 birim/sn tepe üretim; yerel rüzgâr katsayısı ve değişen rüzgâra bağlıdır. Durgun havada üretmez.
- Batarya: 250 birim kapasite. Üretim fazlasını alır; üretim yetmeyince bağlı tüketicileri besler.
- Şarj: en fazla 15 birim/sn. Üç metre içinden terminale nişan alıp E ile bağlanılır. Tekrar E, silah kullanımı, uzaklaşma, toparlanma veya dolumla bağlantı kesilir.
- Kablolar yalnızca bağlı ve çalışır düğümleri aynı devreye alır. Döngülü bağlantılar üretimi çoğaltmaz. Yetersiz elektrik bağlı tüketiciler arasında oransal paylaşılır; kopuk hatlar enerji aktaramaz.
- Silah bataryası boşta bekleyerek dolmaz. Tarayıcı dahil araçlar elektrik kullanır. Karakterin beş saniyelik toparlanması bu bataryayı doldurmaz.

Bir gün varsayılan olarak 15 gerçek dakika sürer. HUD gün, saat ve rüzgârı gösterir. EcoWorldClock üzerinde test için zaman/hava değişimi durdurulabilir. Enerji düzeneği mekanik deneme yerleşimidir; nihai harita aşaması henüz gelmemiştir.

## Kaynaklar ve YEP

Vakum ham metal, plastik ve organik atıkları ayrı toplar. Toplama tek başına geri dönüşüm YEP'i vermez. El geri dönüşüm cihazıyla R, en fazla sekiz birim ham atığı işler; birim başına iki elektrik kullanır. Yetersiz elektrikte yalnızca karşılanabilen miktar işlenir. Metal ve plastik malzemeye, her üç organik atık bir gübreye dönüşür. Üçe tamamlanmayan organik birikim saklanır. Yerdeki atık üzerinde doğrudan geri dönüşüm de aynı malzeme ve YEP hesabını kullanır; basılı tutma süresince elektrik tüketir.

YEP toplamı metal/plastik gibi harcanan malzeme değildir. Seviyeler 1–24 arasındadır. Sonraki seviye için gereken toplam YEP `(seviye - 1) × (40 + 10 × seviye)` olarak hesaplanır. Geri dönüştürülen birim başına 2; dikime 4; temiz hasada 6, kirli hasada 2; kurumuş bitkiyi gübre yapmaya 2; Teneke'ye 12, diğer düşmanları etkisizleştirmeye 8; başarılı yapı kurmaya 12 YEP verilir. Çevre iyileştirme ödülleri ilgili sistem tamamlandığında eklenecektir.

Malzeme harcama tek işlemde doğrulanır: yetersiz veya negatif maliyetler envanterin bir kısmını düşürmez. İnşa ve tesis geliştirmeleri bu API'yi ve YEP seviye kapılarını kullanır. Şebekeye bağlı geri dönüşüm tesisi, su zinciri, sulama ve yapı kademeleri ConstructionAndProduction.md içinde açıklanmıştır.

F5/F9 geçici envanter kaydı artık YEP, organik atık ve kısmi gübre birikimini de içerir. Bu henüz tam dünya kaydı değildir; yapılar, şebeke, çevre ve diğer dünya durumları 11. aşamada birlikte kaydedilecektir.

## Kontroller

Tools/UnityChecks altındaki denetimler yalnızca ayrı Unity test projesine kopyalanarak çalıştırılır; oyun Assets klasörüne test sahnesi veya test çalıştırıcısı eklenmez. RecoveryCheck karakter enerjisini gerçek MainScene'de, PowerCheck üretim/enerji korunumu ve gerçek şarj etkileşimini, ResourceProgressionCheck kaynak işlemlerini ve YEP sınırlarını kontrol eder. PowerVisualCheck mevcut sahne ve HUD'u görüntülemek içindir.
