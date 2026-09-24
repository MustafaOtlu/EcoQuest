# EcoQuest düşman prototipi

MainScene'deki Enemys nesnesinin EnemySceneSetup bileşeni, Play başladığında eski statik yerleşim modellerini gizler ve iskeletli prefabları aynı noktalara yerleştirir. Asit ve Salya için ek deneme konumları tanımlıdır. Edit modunda eski yerleşim işaretleri korunur; prefabların kendileri Assets/Prefabs/Enemies içindedir.

| Tür | Rol | Saldırı | Can |
| --- | --- | --- | --- |
| TinMonster | Yavaş, dayanıklı | Metal mermi, 18 hasar | 180 |
| FlameSpirit | Hızlı atıcı | Ateş topu, 10 hasar | 65 |
| AcidRain | Uzak mesafe | Asit mermisi, 7 hasar ve 2 saniye yavaşlama | 80 |
| SlimeMonster | Yakın takip | Yakın saldırı, 12 hasar ve 1,5 saniye yavaşlama | 90 |
| SmokeCreature | Görüş baskısı | Yakında 2,5 saniyelik duman perdesi, doğrudan hasar yok | 55 |

Düşmanlar çevrelerinde devriye gezer, görüş alanındaki oyuncuyu takip eder, saldırı klibinin yaklaşık %45'inde vuruşu uygular ve bekleme süresinden sonra yeniden saldırır. Oyuncu menzilden kaçarsa veya araya duvar girerse vuruş iptal olur. Uzaktan saldıranlar mesafeyi korur. Oyuncuyu kaybeden veya başlangıç noktasından çok uzaklaşan düşman geri döner. Yere basma, basamaklar ve çarpışmalar CharacterController ile; basit engel ve uçurum kaçınması fizik sorgularıyla yapılır. Bu sistem NavMesh tabanlı tam rota bulma değildir.

Animasyonlar her türün kendi iskeletli Idle FBX modeli üzerinde Generic rig ile çalışır. Root motion kapalıdır; hareket koddan gelir. Locomotion Blend Tree Speed parametresini kullanır; Attack tetikleyicisi tek seferlik saldırıya geçer. Duman için yürüyüş dosyası olmadığından hareket sırasında Idle kullanılır. Ölüm klibi bulunmadığından canı biten düşman kaldırılır.

EnemyBrain bileşenindeki hız, menzil, can, hasar, hazırlık ve bekleme süreleri prefab Inspector'ından değiştirilebilir. Mermiler hızlı geçişlerde çarpışmayı kaçırmamak için her karede katedilen mesafeyi SphereCast ile tarar; atıcıyı yok sayar ve ilk duvarda durur.

PlayerVitals, oyuncuya 100 can, geçici yavaşlama, duman perdesi ve küçük bir can göstergesi ekler. Can biterse oyuncu üç saniye sonra başlangıç noktasında yeniden doğar. Efektler yenilenebilir ama üst üste çarpılarak sınırsız yavaşlatmaz. Yeni silah hasarı EnemyBrain.TakeDamage üzerinden bağlanabilir. Bu görevde oyuncu silahlarının ateş/vakum hasarı, bina hedefleri, ekosistem zararları, ganimet veya boss sistemi eklenmedi.

## Doğrulama

Ayrı bir Unity 6000.0.74f1 test projesinde Play Mode kontrolleri geçti: beş türün kemik animasyonu, üç mermi saldırısı, yakın saldırı, yavaşlatma, duman, takip, eve dönüş, duvar arkasındaki oyuncuyu görmeme, hızlı merminin duvarda durması ve ölümden sonra yeniden doğma. Ana projenin C# derlemesi ve yeni asset referansları ayrıca kontrol edildi. Testler headless çalıştı; MainScene'in son görsel görünümü kullanıcı tarafından Play'de kontrol edilmelidir.
## Güncel silah bağlantısı

Oyuncu silahları artık EnemyBrain.TakeDamage ile bağlıdır; kullanım ve HUD ayrıntıları PlayerWeapons.md içindedir. Sürekli su/anti-vakum hasarı düşmanın saldırı hazırlığını her karede sıfırlamaz.
