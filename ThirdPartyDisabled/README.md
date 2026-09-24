# Devre dışı bırakılan UI demo paketi

Wooden GUI and UIAnimation paketi, ayrı Wooden GUI kaynaklarına ve UIAnimation bileşenlerine referans veren bir entegrasyon/demo paketidir. Projeye gelen sürümde orijinal sprite, nested prefab ve bazı script kaynakları bulunmadığından prefab import hataları oluşuyordu. Reimport eksik bağımlılıkları getirmedi.

Kullanıcının bağımsız HUD tercihi üzerine paket silinmeden bu klasöre taşındı. Unity yalnız Assets altını import ettiğinden bozuk demo artık import edilmez. Taşımadan önce aktif oyun sahneleri, prefablar, animasyonlar ve materyallerin bu pakete referans vermediği kontrol edildi.

Orijinal demo ileride kullanılacaksa önce ana Wooden GUI ve UIAnimation bağımlılıkları temin edilmeli, sonra klasör ve yanındaki .meta dosyası aynı isimlerle Assets altına taşınmalıdır. Eksik kaynakların yerine sahte GUID dosyaları üretilmedi.
