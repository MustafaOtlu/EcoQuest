using System;
using UnityEngine;

/// <summary>
/// Çöp/atık modülü için lokal event bus.
/// _Shared/GameEvents.cs ile aynı pattern'i takip eder ama
/// sadece Bilge'nin waste sistemi tarafından kullanılır.
/// 
/// Global kaynak değişiklikleri (Metal/Plastik ekleme) için
/// GameEvents.OnResourceChanged kullanılmaya devam eder (GameManager üzerinden).
/// </summary>
public static class WasteEvents
{
    /// <summary>Bir çöp toplandığında tetiklenir. Parametre: toplanan çöpün verisi.</summary>
    public static event Action<WasteData> OnWasteCollected;

    /// <summary>Yeni bir çöp haritaya spawn olduğunda tetiklenir.</summary>
    public static event Action<WasteData, Vector3> OnWasteSpawned;

    /// <summary>Çöp envanteri değiştiğinde tetiklenir. Parametre: toplam çöp sayısı.</summary>
    public static event Action<int> OnWasteInventoryChanged;

    /// <summary>Vakum silahı durumu değiştiğinde tetiklenir. Parametre: aktif mi?</summary>
    public static event Action<bool> OnVacuumStateChanged;

    public static void WasteCollected(WasteData data) =>
        OnWasteCollected?.Invoke(data);

    public static void WasteSpawned(WasteData data, Vector3 position) =>
        OnWasteSpawned?.Invoke(data, position);

    public static void WasteInventoryChanged(int totalCount) =>
        OnWasteInventoryChanged?.Invoke(totalCount);

    public static void VacuumStateChanged(bool isActive) =>
        OnVacuumStateChanged?.Invoke(isActive);
}
